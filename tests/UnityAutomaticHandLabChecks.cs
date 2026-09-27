#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;

public partial class UnityAvatarHandLabChecks
{
    IEnumerator AutomaticSetupScenarios()
    {
        var filter=VM(FindObjectOfType<BirdLabFilterControl>());filter.SendCustomEvent("SetRaw");
        var toggle=VM(GameObject.Find("Lab reach toggle").GetComponent<BirdLabToggle>());
        var station=Get<GameObject>(toggle,"target");
        if(!station.activeSelf) toggle.RunEvent("_interact");
        var pointers=new UdonBehaviour[2];
        foreach(var proxy in station.GetComponentsInChildren<BirdAvatarUiInput>(true)) pointers[proxy.input.rightHand?1:0]=VM(proxy.pointer);
        for(int side=0;side<2;side++) inputs[side].SetProgramVariable("automaticSetup",true);
        SetHands(90,1,Quaternion.identity);yield return null;yield return null;
        var rows=new List<string>{"phase,side,bend_deg,scale,learned_axes,manual,ready,fixture_tip_error_m,raw_range_m"};
        for(int side=0;side<2;side++)
        {
            Require(!Get<bool>(inputs[side],"calibrated") && Get<bool>(inputs[side],"tipsReady") && Get<bool>(cursors[side],"poseValid"),"Curled startup works immediately without SET");
            Require(Get<int>(inputs[side],"learnedFingers")==1,"Curled fingers are not recorded as an open hand; only straight thumb learns");
            Require(Get<bool>(pointers[side],"tracked"),"Automatic estimates drive post-IK UI without claiming manual calibration");
        }
        Vector3 savedTarget=target.transform.position;
        for(int side=0;side<2;side++)
        {
            target.transform.position=Vector3.Lerp(Get<Vector3>(cursors[side],"handRoot"),Get<Vector3>(cursors[side],"position"),.5f);yield return null;
            Require(Get<bool>(target,"highlighted"),"Point-through exercise accepts automatic hand "+side);
        }
        target.transform.position=new Vector3(100,-100,100);yield return null;
        Require(!Get<bool>(target,"highlighted"),"Automatic point-through still respects finite logical reach");
        target.transform.position=savedTarget;
        var rigs=new[]{Quaternion.identity,Quaternion.FromToRotation(Vector3.back,Vector3.up),Quaternion.FromToRotation(Vector3.back,Vector3.down),Quaternion.AngleAxis(121,new Vector3(1,3,-2).normalized)};
        foreach(var rig in rigs) foreach(float scale in new[]{.5f,1f,2f}) foreach(float bend in new[]{25f,45,90,150,230})
        {
            for(int side=0;side<2;side++) inputs[side].SendCustomEvent("ResetCalibration");
            SetHands(bend,scale,rig);yield return null;
            for(int side=0;side<2;side++)
            {
                Require(Get<int>(inputs[side],"learnedFingers")==1 && !Get<bool>(inputs[side],"calibrated"),"Cold articulated pose does not learn bent finger axes");
                AutoRecord(rows,"cold_coupled_fixture",side,bend,scale);
                if(bend==230)Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"),0,"Cold fist is exactly coincident with palm");
                Require(!Get<bool>(pointers[side],"hasHistory"),"Restarting estimates cannot synthesize a UI sweep");
            }
        }
        // Parallel phalanges alone are insufficient: a finger folded entirely
        // at the MCP must not be mistaken for an open-hand calibration.
        SetHands(0,1,Quaternion.identity);
        for(int side=0;side<2;side++)
        {
            inputs[side].SendCustomEvent("ResetCalibration");
            var turn=Quaternion.AngleAxis(-90,Vector3.right);
            for(int f=1;f<5;f++)
            {
                int first=1+f*3;Vector3 pivot=positions[Bone(side,first)];
                for(int j=1;j<=2;j++)
                { var bone=Bone(side,first+j);positions[bone]=pivot+turn*(positions[bone]-pivot);rotations[bone]=turn*rotations[bone]; }
                expectedTips[side,f]=pivot+turn*(expectedTips[side,f]-pivot);
            }
        }
        yield return null;
        for(int side=0;side<2;side++)
        { Require(Get<int>(inputs[side],"learnedFingers")==1,"MCP-folded straight segments do not qualify");AutoRecord(rows,"folded_parallel",side,90,1); }
        SetHands(0,1,Quaternion.identity);
        var openPositions=new Dictionary<HumanBodyBones,Vector3>(positions);var openRotations=new Dictionary<HumanBodyBones,Quaternion>(rotations);var openTips=(Vector3[,])expectedTips.Clone();
        SetHands(90,1,Quaternion.identity);yield return null;
        for(int f=1;f<5;f++)
        {
            SetHands(90,1,Quaternion.identity);var revisions=new int[2];
            for(int side=0;side<2;side++)
            {
                revisions[side]=Get<int>(inputs[side],"calibrationRevision");
                for(int j=0;j<3;j++) { var bone=Bone(side,1+f*3+j);positions[bone]=openPositions[bone];rotations[bone]=openRotations[bone]; }
                expectedTips[side,f]=openTips[side,f];
            }
            yield return null;
            for(int side=0;side<2;side++)
            {
                Require(Get<int>(inputs[side],"learnedFingers")==f+1 && !Get<bool>(inputs[side],"calibrated"),"Individual finger learns without whole-hand pose/button");
                Require(Get<int>(inputs[side],"calibrationRevision")==NextUiRevision(revisions[side]),"One learning boundary advances source revision once");
                Require(Get<bool>(pointers[side],"tracked") && !Get<bool>(pointers[side],"hasHistory"),"Learning cancels contact history without dropping usable input");
                AutoRecord(rows,"individual_learning",side,90,1);
            }
        }
        SetHands(90,1,Quaternion.identity);yield return null;yield return null;
        var stableRevisions=new[]{Get<int>(inputs[0],"calibrationRevision"),Get<int>(inputs[1],"calibrationRevision")};
        // Distal rotation varies independently of preceding joint positions.
        // A forever-geometric estimate would miss this; learned local axes must
        // follow it even with arbitrary per-bone bind orientations.
        foreach(var rig in rigs)
        {
            SetHands(90,1,rig);
            for(int side=0;side<2;side++) for(int f=0;f<5;f++)
            {
                var bone=Bone(side,3+f*3);var turn=Quaternion.AngleAxis(17,rig*Vector3.right);
                rotations[bone]=turn*rotations[bone];expectedTips[side,f]=positions[bone]+turn*(expectedTips[side,f]-positions[bone]);
            }
            yield return null;
            for(int side=0;side<2;side++)
            { AutoRecord(rows,"independent_distal_motion",side,90,1);Require(Get<int>(inputs[side],"calibrationRevision")==stableRevisions[side],"Learned axes do not chase ordinary finger motion"); }
        }
        // All required origins pause only the affected hand; learned local axes
        // survive missing samples and recover without another setup action.
        for(int fault=0;fault<16;fault++)
        {
            SetHands(90,1,Quaternion.identity);positions[Bone(0,fault)]=Vector3.zero;yield return null;
            Require(!Get<bool>(inputs[0],"tipsReady") && !Get<bool>(cursors[0],"poseValid") && !Get<bool>(pointers[0],"tracked"),"Automatic loss pauses all consumers");
            Require(Get<int>(inputs[0],"learnedFingers")==5 && Get<bool>(cursors[1],"poseValid"),"Loss retains axes and leaves other hand independent");
            SetHands(90,1,Quaternion.identity);yield return null;
            AutoRecord(rows,"loss_recovery",0,90,1);
            Require(!Get<bool>(pointers[0],"hasHistory"),"Recovered automatic input has no stale contact history");
        }
        inputs[0].enabled=false;yield return null;
        Require(!Get<bool>(inputs[0],"tipsReady") && !Get<Renderer>(views[0],"core").enabled,"Disabled source clears automatic display");
        inputs[0].enabled=true;yield return null;yield return null;
        Require(Get<bool>(cursors[0],"poseValid") && !Get<bool>(inputs[0],"calibrated"),"Re-enable restarts automatically without SET");
        var sender=new ClientSimUdonManagerEventSender(UdonManager.Instance);
        sender.RunEvent("_onAvatarChanged",("player",Networking.LocalPlayer));
        for(int side=0;side<2;side++) Require(Get<int>(inputs[side],"learnedFingers")==0 && !Get<bool>(cursors[side],"poseValid"),"Avatar event clears old axes immediately");
        SetHands(90,1,Quaternion.identity);ChangeAutoBindAxes();yield return null;
        for(int side=0;side<2;side++)AutoRecord(rows,"new_avatar_curled_start",side,90,1);
        SetHands(0,1,Quaternion.identity);ChangeAutoBindAxes();yield return null;
        for(int side=0;side<2;side++)Require(Get<int>(inputs[side],"learnedFingers")==5,"New avatar learns new bone-local axes");
        SetHands(90,1,Quaternion.identity);ChangeAutoBindAxes();yield return null;
        for(int side=0;side<2;side++)AutoRecord(rows,"new_avatar_learned",side,90,1);
        foreach(float bad in new[]{float.NaN,-.1f,1.1f})
        {
            inputs[0].SetProgramVariable("estimatedDistalBendRatio",bad);yield return null;
            Require(!Get<bool>(inputs[0],"dataReady") && !Get<bool>(inputs[0],"tipsReady") && !Get<bool>(cursors[0],"poseValid"),"Invalid automatic estimate settings pause input");
            inputs[0].SetProgramVariable("estimatedDistalBendRatio",.7f);yield return null;
            Require(Get<bool>(cursors[0],"poseValid"),"Valid settings resume automatically");
        }
        // Explicit correction retains the exact original SET endpoint path;
        // AUTO clears that override and immediately resumes usable estimates.
        SetHands(0,1,Quaternion.identity);yield return null;CalibrateControls();yield return null;
        SetHands(90,1,Quaternion.identity);yield return null;
        for(int side=0;side<2;side++) { Require(Get<bool>(inputs[side],"calibrated"),"Optional manual override accepted");AutoRecord(rows,"manual_override",side,90,1); }
        VM(GameObject.Find("RESET LEFT").GetComponent<BirdLabHandControl>()).RunEvent("_interact");yield return null;
        Require(!Get<bool>(inputs[0],"calibrated") && Get<bool>(cursors[0],"poseValid") && Get<bool>(inputs[1],"calibrated"),"AUTO resumes one hand without disturbing the other's override");
        Require(!Get<bool>(pointers[0],"hasHistory"),"AUTO override removal cancels contact history");
        CalibrateControls(false);yield return null;
        for(int side=0;side<2;side++) Require(!Get<bool>(inputs[side],"calibrated") && Get<bool>(cursors[side],"poseValid"),"Failed optional correction never makes SET mandatory");
        toggle.RunEvent("_interact");yield return null;
        filter.SendCustomEvent("SetAdaptive");SetHands(70,1,Quaternion.identity);yield return null;
        Capture("bird-automatic-setup",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        Capture("bird-optional-refine",new Vector3(-.5f,1.65f,-4.5f),new Vector3(-3.35f,1.55f,-1.2f));
        File.WriteAllLines("../Validation/TrackingLab/automatic-setup.csv",rows);
    }
    static void ChangeAutoBindAxes()
    {
        for(int side=0;side<2;side++)for(int f=0;f<5;f++)
        { var bone=Bone(side,3+f*3);rotations[bone]=rotations[bone]*Quaternion.AngleAxis(47+f*7,new Vector3(1,2,3).normalized); }
    }
    void AutoRecord(List<string> rows,string phase,int side,float bend,float scale)
    {
        Require(Get<bool>(inputs[side],"tipsReady") && Get<bool>(cursors[side],"poseValid"),"Automatic/optional source usable: "+phase);
        var tips=Get<Vector3[]>(inputs[side],"estimatedTips");float maxError=0;
        for(int f=0;f<5;f++)maxError=Mathf.Max(maxError,Vector3.Distance(tips[f],expectedTips[side,f]));
        Require(maxError<.0001f,"Known synthetic endpoint agreement (not physical fingertip accuracy): "+phase+" error="+maxError);
        Require(!Get<bool>(cursors[side],"clicksAllowed") && !Get<bool>(cursors[side],"selected"),"Automatic estimates never enable unvalidated clicks");
        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7:R},{8:R}",phase,side,bend,scale,Get<int>(inputs[side],"learnedFingers"),Get<bool>(inputs[side],"calibrated"),Get<bool>(inputs[side],"tipsReady"),maxError,Range(side)));
    }
}
#endif
