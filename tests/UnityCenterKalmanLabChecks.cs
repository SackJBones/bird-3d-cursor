#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using VRC.Udon;

public partial class UnityAvatarHandLabChecks
{
    readonly UdonBehaviour[] centerPolicies=new UdonBehaviour[2];
    readonly List<string> centerMetrics=new List<string>{"case,hz,input_m,value,unit"};
    void CenterMetric(string name,int hz,float input,double value,string unit)
    {
        centerMetrics.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4}",name,hz,input,value,unit));
        Directory.CreateDirectory("../Validation/TrackingLab");
        File.WriteAllLines("../Validation/TrackingLab/center-kalman.csv",centerMetrics);
    }
    static Vector3 CenterReflect(Vector3 v,bool mirror) { return mirror?new Vector3(-v.x,v.y,v.z):v; }
    Vector3 CenterSample(UdonBehaviour vm,Vector3 v,float range,float dt)
    {
        vm.SetProgramVariable("sampleVector",v);vm.SetProgramVariable("sampleRange",range);
        vm.SetProgramVariable("sampleDeltaTime",dt);vm.SendCustomEvent("Step");
        Require(Get<bool>(vm,"valid"),"Center Kalman accepts valid sample");return Get<Vector3>(vm,"vector");
    }
    Vector3 CenterChain(UdonBehaviour center,UdonBehaviour output,Vector3 q,Vector3 root,float dt)
    {
        float rawRange=SphereReach(q.magnitude);
        if(center!=null) q=CenterSample(center,q,rawRange,dt);
        return PolicySample(output,root,SphereWorldPoint(root,q),dt,270*q.magnitude*q.magnitude*q.magnitude);
    }
    void CenterContracts(UdonBehaviour a,UdonBehaviour b,UdonBehaviour downstream,UdonBehaviour baseline)
    {
        // Execute the actual original class as the reference, not a transcription.
        var reference=new KalmanFilterVector3(.000001f,.000009f);
        a.SendCustomEvent("Cancel");
        Vector3 seed=new Vector3(.03f,.08f,.1f);reference.Reset(seed);
        Near(CenterSample(a,seed,100,1f/72),seed,0,"Center first sample seeds exactly");
        float maxParity=0;
        for(int i=0;i<240;i++)
        {
            Vector3 v=seed+new Vector3(.02f*Mathf.Sin(i*.08f),.01f*Mathf.Cos(i*.1f),.005f*Mathf.Sin(i*.2f));
            float error=Vector3.Distance(CenterSample(a,v,100,1f/72),reference.Update(v));
            maxParity=Mathf.Max(maxParity,error);Require(error<.0000001f,"Original Vector3 Kalman recurrence parity");
        }
        CenterMetric("original_kalman_max_error",72,.1f,maxParity,"metres");
        // Rigid motions, mirrored handedness, mixed rates and moving palm roots.
        var random=new System.Random(91402);float maxSymmetry=0;
        for(int trial=0;trial<16;trial++)
        {
            Quaternion rotation=SphereRandomRotation(random);
            Vector3 translation=new Vector3((float)random.NextDouble(),(float)random.NextDouble(),(float)random.NextDouble())*5;
            a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
            downstream.SendCustomEvent("Cancel");baseline.SendCustomEvent("Cancel");
            for(int i=0;i<96;i++)
            {
                Vector3 root=new Vector3(.3f*Mathf.Sin(i*.05f),1,.2f*Mathf.Cos(i*.05f));
                Vector3 q=new Vector3(Mathf.Sin(i*.12f),Mathf.Cos(i*.12f),.4f).normalized*(.02f+.1f*(i%24)/23);
                float dt=1f/(i%3==0?30:i%3==1?72:120);
                // A reflection also commutes with these isotropic Cartesian updates.
                Vector3 first=CenterChain(a,downstream,q,root,dt);
                Vector3 transformed=CenterChain(b,baseline,rotation*CenterReflect(q,trial%2==1),rotation*CenterReflect(root,trial%2==1)+translation,dt);
                float error=Vector3.Distance(transformed,rotation*CenterReflect(first,trial%2==1)+translation)/Mathf.Max(1,first.magnitude);
                maxSymmetry=Mathf.Max(maxSymmetry,error);
                Require(error<.000015f,"Complete center + output chain SE(3)/reflection equivariance");
            }
        }
        CenterMetric("chain_se3_relative_error",72,.12f,maxSymmetry,"ratio");
        // Exactly preserve near-only histories, including translation and closure.
        a.SendCustomEvent("Cancel");downstream.SendCustomEvent("Cancel");baseline.SendCustomEvent("Cancel");
        for(int i=0;i<180;i++)
        {
            Vector3 q=new Vector3(.02f+.01f*Mathf.Sin(i*.1f),.005f,.004f);
            Vector3 root=new Vector3(.2f*Mathf.Sin(i*.03f),1,.1f*Mathf.Cos(i*.04f));
            Near(CenterChain(a,downstream,q,root,1f/72),CenterChain(null,baseline,q,root,1f/72),0,"Nearby chain remains bit-identical");
        }
        foreach(int hz in new[]{30,72,120}) foreach(float length in new[]{.08f,.12f,.3f,2f})
        {
            float dt=1f/hz;Vector3 neutral=Vector3.forward*length;
            a.SendCustomEvent("Cancel");downstream.SendCustomEvent("Cancel");baseline.SendCustomEvent("Cancel");
            for(int i=0;i<hz;i++) { CenterChain(a,downstream,neutral,Vector3.zero,dt);CenterChain(null,baseline,neutral,Vector3.zero,dt); }
            double sumRaw=0,sumNew=0;
            random=new System.Random(41472);int n=hz*3;
            for(int i=0;i<n;i++)
            {
                Vector3 noisy=neutral+new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*length*.04f;
                Vector3 oldPoint=CenterChain(null,baseline,noisy,Vector3.zero,dt);
                Vector3 newPoint=CenterChain(a,downstream,noisy,Vector3.zero,dt);
                Vector3 ideal=SphereWorldPoint(Vector3.zero,neutral);
                sumRaw+=(oldPoint-ideal).sqrMagnitude;sumNew+=(newPoint-ideal).sqrMagnitude;
            }
            double ratio=Math.Sqrt(sumNew/sumRaw);
            CenterMetric("stationary_rms_vs_adaptive",hz,length,ratio,"ratio");
            float effect=Get<float>(a,"influence");
            // Demand at least 12% RMS improvement at full influence, and no
            // noise regression inside the intentional partial blend. Cascading
            // the nonlinear downstream policy is not linear in this weight.
            Require(ratio<(SphereReach(length)>=20?.88:1),"Pre-center noise reduction: hz="+hz+" input="+length+" influence="+effect+" ratio="+ratio);
            a.SendCustomEvent("Cancel");downstream.SendCustomEvent("Cancel");baseline.SendCustomEvent("Cancel");
            for(int i=0;i<hz;i++) { CenterChain(a,downstream,neutral,Vector3.zero,dt);CenterChain(null,baseline,neutral,Vector3.zero,dt); }
            Vector3 target=Vector3.right*length;float arrival=-1,oldArrival=-1,minimum=1;
            for(int i=0;i<hz;i++)
            {
                Vector3 p=CenterChain(a,downstream,target,Vector3.zero,dt);
                Vector3 old=CenterChain(null,baseline,target,Vector3.zero,dt);
                minimum=Mathf.Min(minimum,p.magnitude/SphereReach(length));
                if(arrival<0 && Vector3.Angle(p,target)<9) arrival=(i+1f)/hz;
                if(oldArrival<0 && Vector3.Angle(old,target)<9) oldArrival=(i+1f)/hz;
            }
            CenterMetric("turn_90pct",hz,length,arrival,"seconds");
            CenterMetric("turn_90pct_adaptive_only",hz,length,oldArrival,"seconds");
            CenterMetric("turn_min_range_fraction",hz,length,minimum,"ratio");
            Require(arrival>0 && oldArrival>0 && arrival<=oldArrival+.15f && (SphereReach(length)<20 || arrival<.3f),
                "Bounded added delay: hz="+hz+" input="+length+" new="+arrival+" old="+oldArrival);
            Vector3 near=CenterSample(a,Vector3.forward*.02f, .1f,dt);
            Near(near,Vector3.forward*.02f,0,"Ordinary near return discards distant center history immediately");
        }
        foreach(string key in new[]{"processNoise","measurementNoise","workingRadius","fullEffectRange","sampleDeltaTime"})
        {
            float saved=Get<float>(a,key);a.SetProgramVariable(key,float.NaN);a.SendCustomEvent("Step");
            Require(!Get<bool>(a,"valid"),"Invalid center setting/time rejected: "+key);
            a.SetProgramVariable(key,saved);Near(CenterSample(a,seed,100,1f/72),seed,0,"Fresh recovery without stale center");
        }
        a.SetProgramVariable("sampleVector",new Vector3(float.NaN,0,0));a.SendCustomEvent("Step");
        Require(!Get<bool>(a,"valid"),"Missing center rejected");
        Near(CenterSample(a,seed,100,1f/72),seed,0,"Center loss recovery seeds current geometry");
    }
    IEnumerator CenterKalmanScenarios()
    {
        var control=FindObjectOfType<BirdLabFilterControl>();var vm=VM(control);
        vm.SendCustomEvent("SetRaw");
        CenterContracts(centerPolicies[0],centerPolicies[1],VM(control.adaptiveFilters[0]),VM(control.adaptiveFilters[1]));
        MidRangeContracts(centerPolicies[0],VM(control.adaptiveFilters[0]));
        for(int side=0;side<2;side++)
        {
            inputs[side].SetProgramVariable("automaticSetup",true);
            inputs[side].SendCustomEvent("ResetCalibration");
            cursors[side].SetProgramVariable("centerFilter",centerPolicies[side]);
        }
        SetHands(0,1,Quaternion.identity);yield return null;vm.SendCustomEvent("SetAdaptive");yield return null;
        // Actual saved avatar -> fitter -> center -> limit/range -> output frames.
        foreach(float bend in new[]{90f,60,40,20,5,0,230,90})
        {
            SetHands(bend,1,Quaternion.identity);
            for(int i=0;i<8;i++) yield return null;
            for(int side=0;side<2;side++)
            {
                Require(Get<bool>(cursors[side],"poseValid") && !Get<bool>(inputs[side],"calibrated"),"New chain runs without SET");
                Require(!Get<bool>(cursors[side],"clicksAllowed"),"Center filtering does not enable avatar clicks");
                Require(Get<Renderer>(views[side],"core").enabled,"New chain has visible point");
                Vector3 raw=Get<Vector3>(cursors[side],"rawPosition"),root=Get<Vector3>(cursors[side],"handRoot");
                Vector3 savedFit=Get<Vector3>(fitters[side],"center");
                bool filtered=Get<bool>(cursors[side],"centerFiltered");
                cursors[side].SetProgramVariable("centerFilter",null);cursors[side].SendCustomEvent("Step");
                Near(Get<Vector3>(cursors[side],"rawPosition"),raw,0,"Center stage preserves raw geometric diagnostic");
                Near(Get<Vector3>(fitters[side],"center"),savedFit,0,"Fitted sphere is never overwritten by filtered center");
                cursors[side].SetProgramVariable("centerFilter",centerPolicies[side]);cursors[side].SendCustomEvent("Step");
                if(bend==230) Near(Get<Vector3>(cursors[side],"position"),root,0,"Exact fist after far center history");
                if((raw-root).magnitude<=4) Require(!filtered,"Nearby geometry bypasses the added stage");
            }
        }
        // Normal post-IK source loss/recovery and policy changes clear contact history.
        SetHands(20,1,Quaternion.identity);yield return null;
        var ui=FindObjectsOfType<BirdAvatarUiInput>();
        int revision=Get<int>(cursors[0],"historyRevision");
        centerPolicies[0].SetProgramVariable("measurementNoise",.000016f);yield return null;
        Require(Get<int>(cursors[0],"historyRevision")!=revision,"Center settings propagate history revision");
        foreach(var bridge in ui) if(!bridge.input.rightHand)
            Require(!Get<bool>(VM(bridge.pointer),"hasHistory"),"Policy change cannot sweep through UI");
        centerPolicies[0].SetProgramVariable("measurementNoise",.000009f);
        Vector3 hand=positions[Bone(0,0)];positions[Bone(0,0)]=Vector3.zero;yield return null;
        Require(!Get<bool>(cursors[0],"poseValid") && !Get<bool>(centerPolicies[0],"valid"),"Tracking loss clears center history");
        positions[Bone(0,0)]=hand;yield return null;
        Require(Get<bool>(cursors[0],"poseValid"),"Automatic recovery retains no SET requirement");
        // Bone-space noise, including near-planar fits, through the saved Udon
        // adapter. Mirrored equal inputs compare the new left chain to Lab 13
        // on the right; report remaining limit-law noise rather than hiding it.
        cursors[1].SetProgramVariable("centerFilter",null);
        foreach(float bend in new[]{60f,40,20,5})
        {
            SetHands(0,1,Quaternion.identity);yield return null;
            for(int side=0;side<2;side++) inputs[side].SendCustomEvent("CalibrateOpenHand");
            SetHands(bend,1,Quaternion.identity);
            for(int frame=0;frame<30;frame++) yield return null;
            Vector3[] ideal=new Vector3[2];
            for(int side=0;side<2;side++) ideal[side]=Get<Vector3>(cursors[side],"rawPosition")-Get<Vector3>(cursors[side],"handRoot");
            var random=new System.Random(84172);double[] total=new double[2];int[] invalid=new int[2];
            for(int frame=0;frame<144;frame++)
            {
                SetHands(bend,1,Quaternion.identity);
                for(int bone=1;bone<16;bone++)
                {
                    var noise=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.0006f;
                    positions[Bone(0,bone)]+=noise;
                    positions[Bone(1,bone)]+=CenterReflect(noise,true);
                }
                yield return null;
                for(int side=0;side<2;side++)
                {
                    Require(Get<bool>(cursors[side],"poseValid"),"Noisy near-plane hand retains finite Bird");
                    if(!Get<bool>(fitters[side],"fitValid")) invalid[side]++;
                    Vector3 offset=Get<Vector3>(cursors[side],"position")-Get<Vector3>(cursors[side],"handRoot");
                    total[side]+=(offset-ideal[side]).sqrMagnitude;
                }
            }
            CenterMetric("bone_noise_"+bend+"deg_baseline_range",0,bend,ideal[0].magnitude,"metres");
            CenterMetric("bone_noise_"+bend+"deg_rms_vs_adaptive",0,bend,Math.Sqrt(total[0]/total[1]),"ratio");
            CenterMetric("bone_noise_"+bend+"deg_singular_samples",0,bend,invalid[0],"count");
        }
        cursors[1].SetProgramVariable("centerFilter",centerPolicies[1]);
        var midRangeBones=MidRangeBoneScenarios(); while(midRangeBones.MoveNext()) yield return null;
        for(int side=0;side<2;side++) inputs[side].SendCustomEvent("ResetCalibration");
        SetHands(70,1,Quaternion.identity);yield return null;
        Capture("bird-center-kalman",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        File.WriteAllLines("../Validation/TrackingLab/center-kalman.csv",centerMetrics);
    }
}
#endif
