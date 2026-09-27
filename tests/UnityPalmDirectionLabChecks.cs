#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public partial class UnityAvatarHandLabChecks
{
    IEnumerator DirectionScenarios()
    {
        var control=VM(FindObjectOfType<BirdLabFilterControl>());
        var origins=VM(FindObjectOfType<BirdLabRootControl>());
        Require(Get<bool>(origins,"centered"),"Saved lab starts with palm-centered origin");
        origins.SendCustomEvent("SetClassic");
        control.SendCustomEvent("SetRaw");
        var authoredTilts=new float[2];var authoredPolicies=new bool[2];
        for(int side=0;side<2;side++)
        {
            authoredTilts[side]=Get<float>(cursors[side],"flatDirectionDegrees");
            authoredPolicies[side]=Get<bool>(cursors[side],"useSphereDirection");
            cursors[side].SetProgramVariable("flatDirectionDegrees",0f);
            cursors[side].SetProgramVariable("useSphereDirection",false);
        }
        var rows=new List<string>{"metric,side,tilt_deg,bend_deg,value,unit"};
        var palmUp=Quaternion.FromToRotation(Vector3.back,Vector3.up);
        SetHands(0,1,palmUp);yield return null;CalibrateControls();yield return null;
        // Replay the same fixed-wrist opening under old/new tilt settings.
        // Neither origin nor palm reference can move just because fingers open.
        foreach(float tilt in new[]{45f,0f})
        {
            for(int side=0;side<2;side++) cursors[side].SetProgramVariable("flatDirectionDegrees",tilt);
            var last=new Vector3[2];var roots=new Vector3[2];var maxStep=new float[2];
            for(int i=0;i<=100;i++)
            {
                float bend=50-i*.5f;
                SetHands(bend,1,palmUp);yield return null;
                for(int side=0;side<2;side++)
                {
                    Require(Get<bool>(cursors[side],"poseValid"),"Opening remains valid");
                    Vector3 root=Get<Vector3>(cursors[side],"handRoot"), q=Get<Vector3>(cursors[side],"rangeInput");
                    Near(root,positions[Bone(side,4)]*.6f+positions[Bone(side,1)]*.4f,.000001f,"Ray origin retains reference weighted base formula");
                    Near(Get<Vector3>(inputs[side],"normal"),Vector3.up,.00001f,"Palm-up remains up while fingers open");
                    if(i==0) roots[side]=root;
                    else
                    {
                        Near(root,roots[side],.000001f,"Fixed wrist opening cannot move ray origin");
                        maxStep[side]=Mathf.Max(maxStep[side],Vector3.Angle(last[side],q));
                    }
                    last[side]=q;
                    float departure=Vector3.Angle(q,Vector3.up);
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"palm_departure,{0},{1},{2},{3:R},degrees",side,tilt,bend,departure));
                    if(tilt==0 && bend<=15) Require(Vector3.Dot(q.normalized,Vector3.up)>.99999f,"Open palm-up points up without lateral fallback bias");
                    var cross=Get<LineRenderer>(geometry[side],"rootMarker");
                    Require(cross!=null && cross.enabled,"Ray-origin cross is visible after accepted sample");
                    Near(cross.GetPosition(2),root,.000001f,"Cross marks actual ray origin, not presentation estimate");
                    Near((cross.GetPosition(0)+cross.GetPosition(1))*.5f,root,.000001f,"Origin cross centered across palm");
                    Near((cross.GetPosition(3)+cross.GetPosition(4))*.5f,root,.000001f,"Origin cross centered along palm");
                }
            }
            for(int side=0;side<2;side++)
            {
                rows.Add(string.Format(CultureInfo.InvariantCulture,"max_half_degree_step,{0},{1},0,{2:R},degrees",side,tilt,maxStep[side]));
                Require(maxStep[side]<5,"Fixed-wrist opening has no abrupt direction change in fixture: "+maxStep[side]);
            }
        }
        // Thumb opposition must not rotate the palm frame. It intentionally
        // still changes original root/fit geometry; do not disguise that fact.
        float oldNormalMotion=0;
        foreach(var rotation in new[]{Quaternion.identity,palmUp,Quaternion.FromToRotation(Vector3.back,Vector3.down),Quaternion.AngleAxis(137,new Vector3(1,2,3).normalized)})
        {
            for(int i=0;i<9;i++)
            {
                SetHands(0,1,rotation);
                for(int side=0;side<2;side++)
                {
                    Vector3 shift=rotation*new Vector3(0,.003f*i,.004f*i);
                    for(int bone=1;bone<=3;bone++) positions[Bone(side,bone)]+=shift;
                    Vector3 oldNormal=Vector3.Cross(positions[Bone(side,4)]-positions[Bone(side,1)],positions[Bone(side,13)]-positions[Bone(side,1)]).normalized*(side==0?1:-1);
                    oldNormalMotion=Mathf.Max(oldNormalMotion,Vector3.Angle(oldNormal,expectedPalmNormal));
                }
                yield return null;
                for(int side=0;side<2;side++)
                {
                    Require(Get<bool>(cursors[side],"poseValid"),"Thumb opposition keeps valid palm input");
                    Near(Get<Vector3>(inputs[side],"normal"),expectedPalmNormal,.00001f,"Thumb cannot steer stable palm normal under rigid rotations");
                    Require(Vector3.Dot(Get<Vector3>(cursors[side],"rangeInput").normalized,expectedPalmNormal)>.99999f,"Extended-hand fallback remains palm-outward through thumb motion");
                    Near(Get<Vector3>(cursors[side],"handRoot"),positions[Bone(side,4)]*.6f+positions[Bone(side,1)]*.4f,.000001f,"Thumb contribution to reference root is explicit and preserved");
                }
            }
        }
        rows.Add(string.Format(CultureInfo.InvariantCulture,"old_thumb_normal_max_change,0,0,0,{0:R},degrees",oldNormalMotion));
        Require(oldNormalMotion>20,"Thumb-motion fixture exposes previous normal sensitivity");
        SetHands(0,1,Quaternion.identity);
        Vector3 middle=(positions[Bone(0,4)]+positions[Bone(0,7)]+positions[Bone(0,10)]+positions[Bone(0,13)])*.25f;
        Vector3 across=positions[Bone(0,13)]-positions[Bone(0,4)];
        positions[Bone(0,0)]=middle-across.normalized*.05f;
        yield return null;
        Require(!Get<bool>(inputs[0],"dataReady") && Get<bool>(inputs[0],"calibrated"),"Degenerate palm frame pauses without inventing a world-axis normal");
        Require(!Get<LineRenderer>(geometry[0],"rootMarker").enabled,"Invalid input hides ray-origin cross");
        Require(Get<bool>(cursors[1],"poseValid"),"Other palm remains independent");
        SetHands(0,1,palmUp);yield return null;
        Require(Get<bool>(cursors[0],"poseValid"),"Valid palm resumes without recalibration");
        Capture("bird-palm-direction",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        Directory.CreateDirectory("../Validation/TrackingLab");
        File.WriteAllLines("../Validation/TrackingLab/palm-direction.csv",rows);
        SetHands(0,1,Quaternion.identity);yield return null;CalibrateControls();yield return null;
        var rootChecks=RootScenarios(origins);while(rootChecks.MoveNext()) yield return null;
        for(int side=0;side<2;side++)
        {
            cursors[side].SetProgramVariable("flatDirectionDegrees",authoredTilts[side]);
            cursors[side].SetProgramVariable("useSphereDirection",authoredPolicies[side]);
        }
    }
    IEnumerator RootScenarios(VRC.Udon.UdonBehaviour control)
    {
        var rows=new List<string>{"bend_deg,side,shift_m,fit_center_change_m,bend_change_deg"};
        foreach(float bend in new[]{0f,30,50,90,150,230})
        foreach(var rotation in new[]{Quaternion.identity,Quaternion.AngleAxis(113,new Vector3(1,2,-1).normalized)})
        {
            control.SendCustomEvent("SetClassic");SetHands(bend,1,rotation);yield return null;
            var roots=new Vector3[2];var centers=new Vector3[2];var bends=new float[2];var normals=new Vector3[2];
            for(int side=0;side<2;side++)
            { roots[side]=Get<Vector3>(cursors[side],"handRoot");centers[side]=Get<Vector3>(fitters[side],"center");bends[side]=Get<float>(cursors[side],"bendDegrees");normals[side]=Get<Vector3>(inputs[side],"normal"); }
            Require(control.RunEvent("_interact"),"Native classic-to-palm origin control");yield return null;
            Require(Get<bool>(control,"centered"),"Palm policy selected");
            for(int side=0;side<2;side++)
            {
                Vector3 root=Get<Vector3>(cursors[side],"handRoot"),expected=positions[Bone(side,4)]*.3f+positions[Bone(side,13)]*.3f+positions[Bone(side,1)]*.4f;
                Require(Get<bool>(inputs[side],"calibrated") && Get<bool>(cursors[side],"poseValid"),"Origin change preserves fingertip calibration and valid pose");
                Near(root,expected,.000001f,"Palm origin includes index, pinky and thumb with authored weights");
                Near(root-roots[side],(positions[Bone(side,13)]-positions[Bone(side,4)])*.3f,.000001f,"Origin moves inward along knuckle span");
                float centerChange=Vector3.Distance(Get<Vector3>(fitters[side],"center"),centers[side]);
                Require(centerChange==0,"Origin selection cannot change fitted sphere");
                Near(Get<Vector3>(inputs[side],"normal"),normals[side],0,"Origin selection cannot rotate palm frame");
                Require(Mathf.Abs(Get<float>(cursors[side],"bendDegrees")-bends[side])<.001f,"Explicit thumb base decouples limit pose from ray origin");
                if(Get<float>(cursors[side],"limitWeight")==0 && Get<float>(cursors[side],"fistWeight")==0)
                {
                    Vector3 ray=Get<Vector3>(cursors[side],"rawPosition")-root;
                    Require(Vector3.Dot(ray.normalized,(centers[side]-root).normalized)>.99999f,"Ordinary Bird ray still passes through unchanged fitted center");
                }
                if(bend>=230) Near(Get<Vector3>(cursors[side],"position"),root,0,"Fist returns to newly selected origin");
                Near(Get<LineRenderer>(geometry[side],"rootMarker").GetPosition(2),root,.000001f,"Cross follows selected actual origin");
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R}",bend,side,Vector3.Distance(root,roots[side]),centerChange,Get<float>(cursors[side],"bendDegrees")-bends[side]));
            }
        }
        SetHands(90,1,Quaternion.identity);yield return null;
        control.enabled=false;control.SendCustomEvent("SetClassic");
        Require(Get<bool>(control,"centered"),"Disabled origin control refuses changes");control.enabled=true;
        foreach(float bad in new[]{float.NaN,-.1f,1.1f})
        {
            inputs[0].SetProgramVariable("littleFingerRootShare",bad);yield return null;
            Require(Get<bool>(inputs[0],"calibrated") && !Get<bool>(cursors[0],"poseValid"),"Invalid origin setting pauses without destroying calibration");
            control.SendCustomEvent("SetPalm");yield return null;
            Require(Get<bool>(cursors[0],"poseValid"),"Valid origin setting recovers");
        }
        cursors[0].SetProgramVariable("thumbBase",new Vector3(float.NaN,0,0));cursors[0].SendCustomEvent("Step");
        Require(!Get<bool>(cursors[0],"poseValid"),"Nonfinite explicit thumb base is rejected");yield return null;
        Require(Get<bool>(cursors[0],"poseValid"),"Avatar restores valid explicit palm reference next frame");
        Capture("bird-origin-control",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        File.WriteAllLines("../Validation/TrackingLab/palm-origin.csv",rows);
        control.SendCustomEvent("SetPalm");
    }
}
#endif
