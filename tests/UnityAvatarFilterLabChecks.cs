#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using VRC.Udon;

// Actual compiled Udon policies plus the saved avatar -> cursor -> view pipeline.
public partial class UnityAvatarHandLabChecks
{
    readonly List<string> adaptiveMeasurements=new List<string>{"case,hz,range_m,history_s,value,unit"};
    static float FilterNoise(float range)
    {
        double lo=0,hi=2;
        for(int i=0;i<60;i++)
        {
            double d=(lo+hi)*.5, r=d+d*d/.02+.02*Math.Pow(d/.03,6);
            if(r<range) lo=d; else hi=d;
        }
        float value=(float)((lo+hi)*.5); return 270f*value*value*value;
    }
    Vector3 PolicySample(UdonBehaviour vm,Vector3 root,Vector3 raw,float dt,float noise,bool fist=false)
    {
        vm.SetProgramVariable("sampleRoot",root); vm.SetProgramVariable("samplePosition",raw);
        vm.SetProgramVariable("sampleDeltaTime",dt); vm.SetProgramVariable("sampleNoise",noise);
        vm.SetProgramVariable("sampleFist",fist); vm.SendCustomEvent("Step");
        Require(Get<bool>(vm,"valid"),"Adaptive accepts finite sample");
        return Get<Vector3>(vm,"position");
    }
    void FilterMetric(string name,int hz,float range,int history,double value,string unit)
    { adaptiveMeasurements.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3},{4:R},{5}",name,hz,range,history,value,unit)); }
    void AdaptiveContracts(UdonBehaviour a,UdonBehaviour b)
    {
        Require(a!=b,"Each hand owns separate filter history");
        foreach(int hz in new[]{30,72,120})
        {
            a.SendCustomEvent("Cancel"); float variance=1,maxNearError=0,maxVarianceError=0; Vector3 legacy=Vector3.zero;
            // Same recurrence; C# JIT and VM extern boundaries may round differently.
            for(int i=0;i<hz*3;i++)
            {
                float t=(float)i/hz; var root=new Vector3(.3f*Mathf.Sin(t),1,.2f*Mathf.Cos(t));
                var raw=root+new Vector3(.5f+.3f*Mathf.Sin(2*t),.2f*Mathf.Cos(3*t),2+.2f*Mathf.Cos(t));
                float noise=FilterNoise((raw-root).magnitude);
                if(i==0) legacy=raw;
                else { float predicted=variance+.001f, gain=predicted/(predicted+noise); variance=noise*predicted/(predicted+noise); legacy=legacy*(1-gain)+raw*gain; }
                var actual=PolicySample(a,root,raw,1f/hz,noise);
                float error=Vector3.Distance(actual,legacy), varianceError=Mathf.Abs(Get<float>(a,"variance")-variance);
                maxNearError=Mathf.Max(maxNearError,error);maxVarianceError=Mathf.Max(maxVarianceError,varianceError);
                Require(error<.000001f && varianceError<.000001f,"Near recurrence C#/Udon parity: hz="+hz+" step="+i+" position_error="+error+" variance_error="+varianceError);
            }
            FilterMetric("near_max_error",hz,4,3,maxNearError,"metres");
            FilterMetric("near_max_variance_error",hz,4,3,maxVarianceError,"variance");
            foreach(float range in new[]{1000f,1e9f}) foreach(int history in new[]{1,60})
            {
                a.SendCustomEvent("Cancel"); float noise=FilterNoise(range);
                Vector3 raw=Vector3.right*range;
                for(int i=0;i<hz*history;i++) PolicySample(a,Vector3.zero,raw,1f/hz,noise);
                float minimum=1,arrival=-1;
                for(int i=0;i<hz;i++)
                {
                    var p=PolicySample(a,Vector3.zero,Vector3.forward*range,1f/hz,noise);
                    minimum=Mathf.Min(minimum,p.magnitude/range);
                    if(arrival<0 && Vector3.Angle(p,Vector3.forward)<=9) arrival=(float)(i+1)/hz;
                }
                Require(arrival>0 && arrival<=.15f,"Far angular response bounded after short and long histories");
                Require(minimum>.70f,"Cartesian chord contraction stays above 70 percent for a 90 degree step");
                FilterMetric("turn_90pct",hz,range,history,arrival,"seconds");
                FilterMetric("turn_min_radius_fraction",hz,range,history,minimum,"ratio");
                // Ordinary curl back into reach; no full-fist special case.
                var near=PolicySample(a,Vector3.zero,Vector3.forward*.5f,1f/hz,FilterNoise(.5f));
                Require(near.magnitude<4,"Ordinary near return clears obsolete far state without requiring a fist");
                FilterMetric("first_return",hz,range,history,near.magnitude,"metres");
            }
            // Fixed-seed white angular noise makes the lag/noise tradeoff explicit.
            a.SendCustomEvent("Cancel"); var rng=new System.Random(7349); double inSq=0,outSq=0; int count=0;
            for(int i=0;i<hz*5;i++)
            {
                float angle=(float)(rng.NextDouble()-.5)*.8f;
                var raw=Quaternion.Euler(0,angle,0)*Vector3.forward*1000;
                var p=PolicySample(a,Vector3.zero,raw,1f/hz,FilterNoise(1000));
                if(i>=hz) { inSq+=angle*angle; double output=Math.Atan2(p.x,p.z)*180/Math.PI; outSq+=output*output; count++; }
            }
            Require(outSq<inSq,"Far response still attenuates this synthetic angular noise");
            FilterMetric("jitter_raw_rms",hz,1000,1,Math.Sqrt(inSq/count),"degrees");
            FilterMetric("jitter_filtered_rms",hz,1000,1,Math.Sqrt(outSq/count),"degrees");
        }
        // Rigid/mirrored moving-origin paths, including partial activation and repeated returns.
        foreach(bool mirror in new[]{false,true})
        {
            a.SendCustomEvent("Cancel"); b.SendCustomEvent("Cancel");
            Quaternion rotation=Quaternion.Euler(31,74,-17); Vector3 translation=new Vector3(1,2,-3); float maxError=0;
            for(int i=0;i<360;i++)
            {
                float t=i/72f,range=i%90<30?2:i%90<60?8:1000;
                var root=new Vector3(.4f*Mathf.Sin(t),.2f*Mathf.Cos(t*2),.3f*Mathf.Sin(t*3));
                var raw=root+(Quaternion.Euler(0,t*70,20)*Vector3.forward*range); float noise=FilterNoise(range);
                var p=PolicySample(a,root,raw,1f/72,noise);
                Vector3 mr=root,mx=raw,mp=p;
                if(mirror) { mr.x=-mr.x; mx.x=-mx.x; mp.x=-mp.x; }
                var q=PolicySample(b,rotation*mr+translation,rotation*mx+translation,1f/72,noise);
                float relative=Vector3.Distance(q,rotation*mp+translation)/Mathf.Max(1,p.magnitude);
                maxError=Mathf.Max(maxError,relative);
                Require(relative<.00005f,"Moving-origin filter is rigid and mirror equivariant within float precision");
            }
            FilterMetric(mirror?"mirrored_relative_error":"rigid_relative_error",72,1000,0,maxError,"ratio");
        }
        // Continuity at working-radius join; a far history in the other hand cannot leak.
        a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
        PolicySample(a,Vector3.zero,Vector3.forward*1e9f,1f/72,FilterNoise(1e9f));
        var isolated=PolicySample(b,Vector3.zero,Vector3.right*.1f,1f/72,FilterNoise(.1f));
        Near(isolated,Vector3.right*.1f,0,"Independent hand seeds its own point");
        a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
        PolicySample(a,Vector3.zero,Vector3.forward*3,1f/72,FilterNoise(3));
        PolicySample(b,Vector3.zero,Vector3.forward*3,1f/72,FilterNoise(3));
        var low=PolicySample(a,Vector3.zero,Vector3.forward*3.99999f,1f/72,FilterNoise(4));
        var high=PolicySample(b,Vector3.zero,Vector3.forward*4.00001f,1f/72,FilterNoise(4));
        Near(low,high,.00003f,"No discontinuity at near-volume boundary");
        foreach(float dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity,.251f})
        {
            a.SetProgramVariable("sampleDeltaTime",dt); a.SendCustomEvent("Step");
            Require(!Get<bool>(a,"valid"),"Invalid time rejected");
            var fresh=PolicySample(a,Vector3.one,Vector3.one*2,1f/72,1);
            Near(fresh,Vector3.one*2,0,"Rejected interval clears stale history");
        }
        foreach(string field in new[]{"workingRadius","fullResponseRange","farResponseSeconds","returnMargin","returnContractionRate","sampleNoise"})
        {
            float saved=Get<float>(a,field); a.SetProgramVariable(field,float.NaN);a.SendCustomEvent("Step");
            Require(!Get<bool>(a,"valid"),"Invalid setting/noise rejected: "+field); a.SetProgramVariable(field,saved);
            PolicySample(a,Vector3.zero,Vector3.forward,1f/72,1);
        }
        a.SetProgramVariable("samplePosition",new Vector3(float.NaN,0,0));a.SendCustomEvent("Step");Require(!Get<bool>(a,"valid"),"Nonfinite point rejected");
        PolicySample(a,Vector3.zero,Vector3.forward*1e9f,1f/72,FilterNoise(1e9f));
        Near(PolicySample(a,Vector3.one,Vector3.one,1f/72,0,true),Vector3.one,0,"Fist endpoint exactly coincides with hand");
        Require(Get<float>(a,"influence")==0 && Get<float>(a,"variance")==1,"Fist clears adaptive history");
        a.enabled=false; a.SendCustomEvent("Step"); Require(!Get<bool>(a,"valid"),"Disabled policy refuses queued step");a.enabled=true;
        Near(PolicySample(a,Vector3.zero,Vector3.forward,1f/72,1),Vector3.forward,0,"Reenabled policy seeds fresh");
        a.SetProgramVariable("historyRevision",int.MaxValue);a.SendCustomEvent("Cancel");Require(Get<int>(a,"historyRevision")==0,"Policy revision wraps");
        a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
        Directory.CreateDirectory("../Validation/TrackingLab");File.WriteAllLines("../Validation/TrackingLab/range-adaptive-filter.csv",adaptiveMeasurements);
    }
    IEnumerator AdaptiveScenarios()
    {
        var control=FindObjectOfType<BirdLabFilterControl>(); var vm=VM(control);
        Require(control.adaptiveFilters.Length==2,"Saved comparison has two optional policies");
        var a=VM(control.adaptiveFilters[0]); var b=VM(control.adaptiveFilters[1]);
        vm.SendCustomEvent("SetRaw"); AdaptiveContracts(a,b);
        SetHands(0,1,Quaternion.identity);yield return null; CalibrateControls();yield return null;
        Require(vm.RunEvent("_interact"),"Native RAW -> original filter");yield return null;
        Require(Get<bool>(vm,"filtered") && !Get<bool>(vm,"adaptive"),"Original filter retained");
        Require(vm.RunEvent("_interact"),"Native original -> adaptive");yield return null;
        Require(Get<bool>(vm,"adaptive"),"Adaptive policy selected");
        for(int side=0;side<2;side++)
        {
            Require(Get<bool>(cursors[side],"poseValid") && !Get<bool>(cursors[side],"clicksAllowed"),"Adaptive mode keeps valid avatar sample and disables inferred clicks");
            Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"rawPosition"),.001f,"Adaptive mode starts at current raw point");
        }
        SetHands(0,1,Quaternion.Euler(0,90,0));
        for(int frame=0;frame<18;frame++) yield return null;
        for(int side=0;side<2;side++)
        {
            var root=Get<Vector3>(cursors[side],"handRoot");
            Require(Vector3.Angle(Get<Vector3>(cursors[side],"position")-root,Get<Vector3>(cursors[side],"rawPosition")-root)<9,"Wrist turn reaches raw direction in normal post-IK frames");
        }
        SetHands(90,1,Quaternion.identity);yield return null;
        for(int side=0;side<2;side++)
        {
            Require(Range(side)<4 && Vector3.Distance(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"))<4,"Normal avatar curl returns into working volume");
            RecordTemporal("adaptive_return",side);
        }
        SetHands(0,1,Quaternion.identity);yield return null;
        SetHands(230,1,Quaternion.identity);yield return null;
        for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"),.00001f,"Adaptive fist stays at hand");
        SetHands(0,1,Quaternion.identity);yield return null;
        SetHands(90,1,Quaternion.identity);inputs[0].SetProgramVariable("lastSampleTime",Time.realtimeSinceStartup-1);yield return null;
        Require(Get<bool>(cursors[0],"poseValid"),"Adaptive gap recovery accepts first resumed sample");
        Near(Get<Vector3>(cursors[0],"position"),Get<Vector3>(cursors[0],"rawPosition"),.00001f,"Gap recovery seeds raw rather than integrating suspension");
        cursors[0].SetProgramVariable("smoothing",false);yield return null;
        Require(!Get<bool>(a,"valid"),"Direct smoothing bypass clears helper history");
        cursors[0].SetProgramVariable("smoothing",true);yield return null;
        Near(Get<Vector3>(cursors[0],"position"),Get<Vector3>(cursors[0],"rawPosition"),.00001f,"Direct adaptive resume seeds fresh");
        vm.SendCustomEvent("SetAdaptive");Require(Get<bool>(cursors[0],"poseValid"),"Idempotent mode request preserves sample");
        Capture("bird-adaptive-control",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        Require(vm.RunEvent("_interact"),"Native adaptive -> sphere");yield return null;
        Require(Get<bool>(vm,"sphere"),"Sphere experiment follows adaptive");
        Require(vm.RunEvent("_interact"),"Native sphere -> RAW");yield return null;
        Require(!Get<bool>(vm,"filtered"),"Native four-mode cycle returns to RAW");
    }
}
#endif
