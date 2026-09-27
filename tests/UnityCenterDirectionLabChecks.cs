#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public partial class UnityAvatarHandLabChecks
{
    IEnumerator CenterDirectionScenarios()
    {
        VM(FindObjectOfType<BirdLabFilterControl>()).SendCustomEvent("SetRaw");
        VM(FindObjectOfType<BirdLabRootControl>()).SendCustomEvent("SetPalm");
        for(int side=0;side<2;side++) Require(Get<bool>(cursors[side],"useSphereDirection") && Get<float>(cursors[side],"flatDirectionDegrees")==45,"Sphere-directed aim with 45-degree pathological fallback");
        SetHands(0,1,Quaternion.identity);yield return null;CalibrateControls();yield return null;
        var rows=new List<string>{"case,side,pose,range_m,center_normal_dot,correction_weight,relative_range_error,direction_error_deg"};
        var rigs=new[]{Quaternion.identity,Quaternion.FromToRotation(Vector3.back,Vector3.up),Quaternion.FromToRotation(Vector3.back,Vector3.down),Quaternion.AngleAxis(137,new Vector3(1,2,3).normalized)};
        int front=0,behind=0,singular=0;
        foreach(var rig in rigs)
        foreach(float bend in new[]{-15f,-5,0,5,15,25,40,60,90,150,210,230})
        {
            SetHands(bend,1,rig);yield return null;
            for(int side=0;side<2;side++) CheckCenterDirection(side,"articulation",bend,rows,ref front,ref behind,ref singular);
        }
        // Reshape only distal/intermediate finger points and orientations. Wrist,
        // knuckles, thumb and ray origin remain fixed while the distant aim moves.
        var first=new Vector3[2];var roots=new Vector3[2];var maxTurn=new float[2];var usable=new int[2];
        for(int i=-12;i<=12;i++)
        {
            SetHands(20,1,Quaternion.identity);
            for(int side=0;side<2;side++)
            {
                var bend=Quaternion.AngleAxis(i*2*(side==0?1:-1),Vector3.back);
                for(int f=2;f<=3;f++)
                {
                    int k=1+f*3;Vector3 pivot=positions[Bone(side,k)];
                    for(int j=1;j<=2;j++)
                    {
                        var bone=Bone(side,k+j);positions[bone]=pivot+bend*(positions[bone]-pivot);rotations[bone]=bend*rotations[bone];
                    }
                }
            }
            yield return null;
            for(int side=0;side<2;side++)
            {
                Vector3 root=Get<Vector3>(cursors[side],"handRoot");
                if(i==-12)roots[side]=root;else Near(root,roots[side],0,"Finger-only reshape leaves palm origin fixed");
                Near(Get<Vector3>(inputs[side],"normal"),Vector3.back,.00001f,"Finger-only reshape leaves palm frame fixed");
                CheckCenterDirection(side,"finger_steering",i,rows,ref front,ref behind,ref singular);
                Vector3 direction=(Get<Vector3>(cursors[side],"rawPosition")-root).normalized;
                if(Get<bool>(fitters[side],"fitValid") && Get<float>(cursors[side],"insideOutWeight")==0 && Range(side)>25)
                {
                    if(usable[side]++==0)first[side]=direction;else maxTurn[side]=Mathf.Max(maxTurn[side],Vector3.Angle(first[side],direction));
                }
            }
        }
        for(int side=0;side<2;side++) Require(usable[side]>=3 && maxTurn[side]>3,"Far Bird responds to fingers without wrist motion: samples="+usable[side]+" turn="+maxTurn[side]);
        // Sweep a known center-relative direction across the palm plane. Changing
        // the synthetic root isolates the direction law from the sphere fitter.
        foreach(var rig in rigs)
        {
            SetHands(70,1,rig);yield return null;
            for(int side=0;side<2;side++)
            {
                Vector3 center=Get<Vector3>(fitters[side],"center"),normal=Get<Vector3>(inputs[side],"normal");
                Require(Get<bool>(fitters[side],"fitValid"),"Sweep has real fitted center");
                Vector3 axis=(positions[Bone(side,13)]-positions[Bone(side,4)]).normalized;
                if(Vector3.Dot(Vector3.Cross(axis,normal),rig*Vector3.up)<0)axis=-axis;
                Vector3 previous=Vector3.zero;float maxStep=0;
                for(int i=0;i<=200;i++)
                {
                    float turn=60+i*.3f;Vector3 desired=Quaternion.AngleAxis(turn,axis)*normal;
                    cursors[side].SetProgramVariable("handRoot",center-desired*.07f);
                    CheckCenterDirection(side,"palm_crossing",turn,rows,ref front,ref behind,ref singular);
                    Vector3 result=Get<Vector3>(cursors[side],"rangeInput").normalized;
                    if(i>0)maxStep=Mathf.Max(maxStep,Vector3.Angle(previous,result));previous=result;
                }
                Require(maxStep<3,"Behind-palm transition continuous at .3-degree samples: "+maxStep);
            }
        }
        Require(front>0 && behind>0 && singular>0,"Direction checks cover healthy, inside-out and singular fits");
        foreach(float bad in new[]{0f,-1,float.NaN,1.1f})
        {
            cursors[0].SetProgramVariable("insideOutFullBlend",bad);yield return null;
            Require(!Get<bool>(cursors[0],"poseValid"),"Invalid correction depth rejected");
            cursors[0].SetProgramVariable("insideOutFullBlend",.25f);yield return null;
            Require(Get<bool>(cursors[0],"poseValid"),"Valid correction recovers without SET");
        }
        SetHands(230,1,Quaternion.identity);yield return null;
        for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"),0,"Direction correction retains exact fist endpoint");
        VM(FindObjectOfType<BirdLabFilterControl>()).SendCustomEvent("SetAdaptive");
        SetHands(40,1,Quaternion.identity);yield return null;
        Capture("bird-center-direction",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        File.WriteAllLines("../Validation/TrackingLab/center-direction.csv",rows);
    }
    void CheckCenterDirection(int side,string name,float pose,List<string> rows,ref int front,ref int behind,ref int singular)
    {
        var cursor=cursors[side];
        cursor.SetProgramVariable("useSphereDirection",false);cursor.SetProgramVariable("flatDirectionDegrees",0f);cursor.SendCustomEvent("Step");
        Require(Get<bool>(cursor,"poseValid"),"Untiltted radial reference valid");
        Vector3 root=Get<Vector3>(cursor,"handRoot");float range=(Get<Vector3>(cursor,"rawPosition")-root).magnitude;
        cursor.SetProgramVariable("useSphereDirection",true);cursor.SetProgramVariable("flatDirectionDegrees",45f);cursor.SendCustomEvent("Step");
        Require(Get<bool>(cursor,"poseValid"),"Sphere direction valid");
        Vector3 q=Get<Vector3>(cursor,"rangeInput"),normal=Get<Vector3>(inputs[side],"normal");
        Vector3 axis=(positions[Bone(side,13)]-positions[Bone(side,4)]).normalized;
        Vector3 palmForward=(positions[Bone(side,4)]+positions[Bone(side,7)]+positions[Bone(side,10)]+positions[Bone(side,13)])*.25f-positions[Bone(side,1)];
        if(Vector3.Dot(Vector3.Cross(axis,normal),palmForward)<0)axis=-axis;
        Vector3 fallback=Quaternion.AngleAxis(45,axis)*normal,expected=fallback;
        float dot=0,weight=Get<float>(cursor,"insideOutWeight");
        if(Get<bool>(fitters[side],"fitValid"))
        {
            Vector3 fit=(Get<Vector3>(fitters[side],"center")-root).normalized;dot=Vector3.Dot(fit,normal);
            if(dot>=0)
            {
                front++;Require(weight==0,"Every front-side fit retains sphere-center aim irrespective of range/confidence");expected=fit;
            }
            else
            {
                behind++;float t=Mathf.Clamp01(-dot/.25f);float w=t*t*(3-2*t);
                Require(Mathf.Abs(weight-w)<.00001f,"Correction depends on signed center depth only");
                expected=Vector3.Slerp(fit,fallback,w).normalized;
            }
        }
        else { singular++;Require(weight==1,"Singular fit reports fallback explicitly"); }
        if(q.sqrMagnitude>0) Near(q.normalized,expected,.00003f,"Ray is through sphere center except measured inside-out/singular cases");
        float error=Mathf.Abs((Get<Vector3>(cursor,"rawPosition")-root).magnitude-range)/Mathf.Max(.001f,range);
        Require(error<.00001f,"Direction choice preserves existing scalar range: "+error);
        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R},{6:R},{7:R}",name,side,pose,range,dot,weight,error,q.sqrMagnitude>0?Vector3.Angle(q,expected):0));
    }
}
#endif
