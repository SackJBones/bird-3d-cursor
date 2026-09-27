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
    readonly List<string> sphereMeasurements=new List<string>{"metric,hz,sphere_distance_m,angle_deg,value,unit"};
    void SphereMetric(string metric,int hz,float s,float angle,double value,string unit)
    { sphereMeasurements.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5}",metric,hz,s,angle,value,unit)); }
    static float SphereReach(float s)
    { float n=s/.02f,f=s/.03f; return (n+n*n+f*f*f*f*f*f)*.02f; }
    Vector3 SphereSample(UdonBehaviour vm,Vector3 q,float dt,Vector3 normal,Vector3 forward)
    {
        vm.SetProgramVariable("sampleVector",q); vm.SetProgramVariable("sampleDeltaTime",dt);
        vm.SetProgramVariable("sampleNormal",normal); vm.SetProgramVariable("sampleForward",forward);
        vm.SendCustomEvent("Step"); Require(Get<bool>(vm,"valid"),"Sphere policy accepts finite sample");
        Vector3 result=Get<Vector3>(vm,"vector");
        Require(!float.IsNaN(result.sqrMagnitude) && !float.IsInfinity(result.sqrMagnitude),"Sphere state remains finite");
        return result;
    }
    Vector3 SphereSample(UdonBehaviour vm,Vector3 q,float dt)
    { return SphereSample(vm,q,dt,Vector3.up,Vector3.forward); }
    static Quaternion SphereRandomRotation(System.Random random)
    {
        // Uniform SO(3) fixture, without a yaw/pitch/roll chart.
        double u=random.NextDouble(),v=random.NextDouble()*2*Math.PI,w=random.NextDouble()*2*Math.PI;
        return new Quaternion((float)(Math.Sqrt(1-u)*Math.Sin(v)),(float)(Math.Sqrt(1-u)*Math.Cos(v)),
            (float)(Math.Sqrt(u)*Math.Sin(w)),(float)(Math.Sqrt(u)*Math.Cos(w))).normalized;
    }
    static Vector3 SphereWorldPoint(Vector3 root,Vector3 q)
    { float s=q.magnitude;return s>0?root+q/s*SphereReach(s):root; }
    void SphereSymmetries(UdonBehaviour a,UdonBehaviour b)
    {
        var random=new System.Random(27619);
        float maxVectorError=0,maxWorldError=0,maxGainError=0,maxNearAbsolute=0;
        for(int trial=0;trial<32;trial++)
        {
            Quaternion rotation=trial==0?Quaternion.identity:trial==1?Quaternion.FromToRotation(Vector3.forward,Vector3.up):
                trial==2?Quaternion.FromToRotation(Vector3.forward,Vector3.down):SphereRandomRotation(random);
            Vector3 translation=new Vector3((float)random.NextDouble()*6-3,(float)random.NextDouble()*6-3,(float)random.NextDouble()*6-3);
            if(trial%8==0) translation*=250; // include floating-origin stress
            a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
            for(int i=0;i<144;i++)
            {
                float t=i*.11f,s=new[]{.00001f,.005f,.02f,.07f,.12f,2}[(i/24)%6];
                int phase=i%24;
                Vector3 direction=phase==0?Vector3.forward:phase==1?Vector3.back:phase==2?Vector3.up:phase==3?Vector3.down:
                    phase==4?Vector3.right:phase==5?Vector3.left:phase==6?new Vector3(1e-8f,1,1e-8f):
                    phase==7?new Vector3(-1e-8f,1,-1e-8f):new Vector3(Mathf.Sin(t),Mathf.Cos(t),.2f*Mathf.Cos(t*2));
                Vector3 q=phase==23?Vector3.zero:direction.normalized*s;
                Vector3 root=new Vector3(.4f*Mathf.Sin(t),1+.2f*Mathf.Cos(t),.3f*Mathf.Cos(t*2));
                float dt=1f/(i%3==0?30:i%3==1?72:120);
                var original=SphereSample(a,q,dt);
                var transformed=SphereSample(b,rotation*q,dt,rotation*Vector3.up,rotation*Vector3.forward);
                float vectorError=Vector3.Distance(transformed,rotation*original)/Mathf.Max(.0001f,original.magnitude);
                float gainError=Mathf.Abs(Get<float>(a,"gain")-Get<float>(b,"gain"));
                Vector3 expected=rotation*SphereWorldPoint(root,original)+translation;
                Vector3 actual=SphereWorldPoint(rotation*root+translation,transformed);
                float range=SphereReach(original.magnitude),worldScale=Mathf.Max(1,range,(rotation*root+translation).magnitude);
                float absolute=Vector3.Distance(actual,expected),worldError=absolute/worldScale;
                maxVectorError=Mathf.Max(maxVectorError,vectorError);maxWorldError=Mathf.Max(maxWorldError,worldError);
                maxGainError=Mathf.Max(maxGainError,gainError);
                Require(vectorError<.000006f && gainError<.00003f,"SO(3) commutes with tangent-space smoothing: trial="+trial+" sample="+i+" vector="+vectorError+" gain="+gainError);
                Require(worldError<.00001f,"SE(3) commutes with the reconstructed logical Bird: trial="+trial+" sample="+i+" relative="+worldError);
                if(range<=4 && translation.magnitude<10)
                {
                    maxNearAbsolute=Mathf.Max(maxNearAbsolute,absolute);
                    Require(absolute<.000025f,"Near SE(3) error stays below 25 micrometres");
                }
            }
        }
        SphereMetric("se3_vector_relative_error",72,2,0,maxVectorError,"ratio");
        SphereMetric("se3_world_relative_error",72,2,0,maxWorldError,"ratio");
        SphereMetric("se3_gain_error",72,2,0,maxGainError,"ratio");
        SphereMetric("se3_near_absolute_error",72,.07f,0,maxNearAbsolute,"metres");
        // Separately include cancellation/rounding from absolute float centers.
        // This tests the caller's c-root conversion, beyond the vector policy API.
        float maxCenterError=0;
        for(int trial=0;trial<8;trial++)
        {
            Quaternion rotation=SphereRandomRotation(random);
            Vector3 translation=new Vector3((float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2);
            a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
            for(int i=0;i<96;i++)
            {
                float t=i*.12f;
                Vector3 root=new Vector3(.3f*Mathf.Sin(t),1+.1f*Mathf.Cos(t),.2f*Mathf.Cos(t*2));
                Vector3 q=new Vector3(Mathf.Sin(t),Mathf.Cos(t),.1f).normalized*(.02f+.05f*(.5f+.5f*Mathf.Sin(t)));
                Vector3 center=root+q,changedRoot=rotation*root+translation,changedCenter=rotation*center+translation;
                var original=SphereSample(a,center-root,1f/72);
                var changed=SphereSample(b,changedCenter-changedRoot,1f/72,rotation*Vector3.up,rotation*Vector3.forward);
                float error=Vector3.Distance(SphereWorldPoint(changedRoot,changed),rotation*SphereWorldPoint(root,original)+translation);
                maxCenterError=Mathf.Max(maxCenterError,error);
                Require(error<.001f,"SE(3) including absolute-center float subtraction stays within 1 mm in working volume");
            }
        }
        SphereMetric("se3_absolute_center_error",72,.07f,0,maxCenterError,"metres");
    }
    void SphereContracts(UdonBehaviour a,UdonBehaviour b)
    {
        Require(a!=b,"Separate sphere state per hand");
        foreach(int hz in new[]{30,72,120})
        {
            foreach(float angle in new[]{1f,90,170,180})
            {
                float minArrival=1,maxArrival=0;
                foreach(float s in new[]{.00001f,.005f,.02f,.07f,.12f,2})
                {
                    a.SendCustomEvent("Cancel");
                    for(int i=0;i<hz;i++) SphereSample(a,Vector3.forward*s,1f/hz);
                    Vector3 target=angle==180?Vector3.back*s:Quaternion.Euler(0,angle,0)*Vector3.forward*s;
                    float arrival=-1,minimum=1,maximum=1;
                    for(int i=0;i<hz/2;i++)
                    {
                        Vector3 q=SphereSample(a,target,1f/hz);
                        float ratio=SphereReach(q.magnitude)/SphereReach(s);
                        minimum=Mathf.Min(minimum,ratio);maximum=Mathf.Max(maximum,ratio);
                        if(arrival<0 && Vector3.Angle(q,target)<=angle*.1f+.015f) arrival=(i+1f)/hz;
                    }
                    Require(arrival>0 && arrival<=.14f,"Sphere turning response bounded at all ranges including antipodes");
                    Require(minimum>.99998f && maximum<1.00002f,"Pure turn preserves polynomial reach within float precision");
                    minArrival=Mathf.Min(minArrival,arrival);maxArrival=Mathf.Max(maxArrival,arrival);
                    SphereMetric("turn_90pct",hz,s,angle,arrival,"seconds");
                    SphereMetric("turn_min_radius_fraction",hz,s,angle,minimum,"ratio");
                    SphereMetric("turn_max_radius_fraction",hz,s,angle,maximum,"ratio");
                }
                Require(maxArrival-minArrival<1.01f/hz,"Angular response does not grow with range");
            }
            foreach(float s in new[]{.02f,.07f,.12f})
            {
                a.SendCustomEvent("Cancel");var random=new System.Random(7392);double rawSq=0,shownSq=0,rawAngleSq=0,shownAngleSq=0;int count=0;
                for(int i=0;i<hz*5;i++)
                {
                    float noise=(float)(random.NextDouble()-.5)*.0006928203f;
                    float angle=(float)(random.NextDouble()-.5)*.17320508f;
                    var raw=Quaternion.Euler(0,angle,0)*Vector3.forward*(s+noise);
                    var shown=SphereSample(a,raw,1f/hz);
                    if(i>=hz)
                    {
                        double dr=SphereReach(raw.magnitude)-SphereReach(s),df=SphereReach(shown.magnitude)-SphereReach(s);
                        double af=Math.Atan2(shown.x,shown.z)*180/Math.PI;
                        rawSq+=dr*dr;shownSq+=df*df;rawAngleSq+=angle*angle;shownAngleSq+=af*af;count++;
                    }
                }
                Require(shownSq<rawSq && shownAngleSq<rawAngleSq,"Sphere filter attenuates synthetic radial and angular noise");
                SphereMetric("jitter_raw_radial",hz,s,0,Math.Sqrt(rawSq/count),"metres");
                SphereMetric("jitter_filtered_radial",hz,s,0,Math.Sqrt(shownSq/count),"metres");
                SphereMetric("jitter_raw_angle",hz,s,0,Math.Sqrt(rawAngleSq/count),"degrees");
                SphereMetric("jitter_filtered_angle",hz,s,0,Math.Sqrt(shownAngleSq/count),"degrees");
            }
            a.SendCustomEvent("Cancel");SphereSample(a,Vector3.forward*2,1f/hz);
            var near=SphereSample(a,Vector3.forward*.02f,1f/hz);
            Require(SphereReach(near.magnitude)<1,"Ordinary return to nearby fit sheds extreme history in first sample");
            SphereMetric("first_return",hz,.02f,0,SphereReach(near.magnitude),"metres");
        }
        // With the same adaptive coefficients disabled, compare the local
        // response along radial and both tangential axes at a nonzero point.
        a.SetProgramVariable("radialSpeedCoefficient",0f);a.SetProgramVariable("angularSpeedCoefficient",0f);
        float alpha=1/(1+1/(2*Mathf.PI*4/72));
        foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})
        {
            a.SendCustomEvent("Cancel");SphereSample(a,Vector3.forward*.02f,1f/72);
            var actual=SphereSample(a,Vector3.forward*.02f+axis*.000001f,1f/72);
            Near(actual,Vector3.forward*.02f+axis*.000001f*alpha,.000000004f,"Local first-order response is isotropic with common gain");
        }
        a.SetProgramVariable("radialSpeedCoefficient",100f);a.SetProgramVariable("angularSpeedCoefficient",4f);
        // Includes near-zero crossings, exact antipodes and a moving palm frame.
        foreach(bool mirror in new[]{false,true})
        {
            a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");float maxError=0;
            Quaternion rig=Quaternion.Euler(31,74,-17);
            for(int i=0;i<240;i++)
            {
                float s=i%60<20?.02f:i%60<40?.12f:2;
                Vector3 q=Quaternion.Euler(i*.7f,i*2,0)*Vector3.forward*s;
                if(i%60==0) q=Vector3.forward*.02f;
                if(i%60==1) q=Vector3.back*.02f;
                if(i%60==2) q=Vector3.zero;
                if(i%60==3) q=Vector3.right*1e-8f;
                Vector3 n=Vector3.up,f=Vector3.forward;
                var p=SphereSample(a,q,1f/72,n,f);
                if(mirror) { q.x=-q.x;n.x=-n.x;f.x=-f.x;p.x=-p.x; }
                var transformed=SphereSample(b,rig*q,1f/72,rig*n,rig*f);
                float error=Vector3.Distance(transformed,rig*p)/Mathf.Max(.0001f,p.magnitude);
                maxError=Mathf.Max(maxError,error);
                Require(error<.00004f,"Sphere interpolation respects rotation/mirroring including palm-defined antipodes");
            }
            SphereMetric(mirror?"mirrored_relative_error":"rigid_relative_error",72,2,0,maxError,"ratio");
        }
        SphereSymmetries(a,b);
        a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
        SphereSample(a,Vector3.forward*2,1f/72);
        Near(SphereSample(b,Vector3.right*.01f,1f/72),Vector3.right*.01f,0,"Other hand seeds independently");
        Near(SphereSample(a,Vector3.zero,1f/72),Vector3.zero,0,"Exact fist clears distant state");
        Near(SphereSample(a,Vector3.left*1e-8f,1f/72),Vector3.left*1e-8f,0,"Leaving origin uses new direction without normalized-vector dead zone");
        foreach(float dt in new[]{0f,-1,float.NaN,float.PositiveInfinity,.251f})
        {
            a.SetProgramVariable("sampleDeltaTime",dt);a.SendCustomEvent("Step");Require(!Get<bool>(a,"valid"),"Invalid time rejected");
            Near(SphereSample(a,Vector3.forward*.03f,1f/72),Vector3.forward*.03f,0,"Time discontinuity clears old history");
        }
        foreach(string field in new[]{"minimumCutoff","radialSpeedCoefficient","angularSpeedCoefficient","derivativeCutoff"})
        {
            float saved=Get<float>(a,field);a.SetProgramVariable(field,float.NaN);a.SendCustomEvent("Step");
            Require(!Get<bool>(a,"valid"),"Invalid sphere setting rejected: "+field);a.SetProgramVariable(field,saved);
            Near(SphereSample(a,Vector3.right*.02f,1f/72),Vector3.right*.02f,0,"Setting recovery seeds fresh");
        }
        foreach(string field in new[]{"sampleVector","sampleNormal","sampleForward"})
        {
            a.SetProgramVariable(field,new Vector3(float.NaN,0,0));a.SendCustomEvent("Step");Require(!Get<bool>(a,"valid"),"Nonfinite vector rejected: "+field);
            SphereSample(a,Vector3.forward*.02f,1f/72);
        }
        a.SetProgramVariable("sampleVector",Vector3.one*float.MaxValue);a.SendCustomEvent("Step");Require(!Get<bool>(a,"valid"),"Overflowing radius rejected");
        SphereSample(a,Vector3.forward*.02f,1f/72);
        a.SetProgramVariable("sampleVector",Vector3.back*.02f);a.SetProgramVariable("sampleNormal",Vector3.forward);a.SetProgramVariable("sampleForward",Vector3.forward);
        a.SendCustomEvent("Step");Require(!Get<bool>(a,"valid"),"Undefined antipode axis rejected instead of inventing world-up dependence");
        SphereSample(a,Vector3.forward*.02f,1f/72);
        a.enabled=false;a.SendCustomEvent("Step");Require(!Get<bool>(a,"valid"),"Disabled sphere policy refuses queued work");a.enabled=true;
        Near(SphereSample(a,Vector3.right*.02f,1f/72),Vector3.right*.02f,0,"Reenabled sphere policy seeds fresh");
        a.SetProgramVariable("historyRevision",int.MaxValue);a.SendCustomEvent("Cancel");Require(Get<int>(a,"historyRevision")==0,"Sphere revision wraps");
        a.SendCustomEvent("Cancel");b.SendCustomEvent("Cancel");
        Directory.CreateDirectory("../Validation/TrackingLab");File.WriteAllLines("../Validation/TrackingLab/sphere-space-filter.csv",sphereMeasurements);
    }
    IEnumerator SphereScenarios()
    {
        var control=FindObjectOfType<BirdLabFilterControl>();var vm=VM(control);
        Require(control.sphereFilters.Length==2,"Saved sphere comparison has two policies");
        var a=VM(control.sphereFilters[0]);var b=VM(control.sphereFilters[1]);
        vm.SendCustomEvent("SetRaw");SphereContracts(a,b);
        SetHands(0,1,Quaternion.identity);yield return null;CalibrateControls();yield return null;
        vm.SendCustomEvent("SetSphere");yield return null;
        Require(Get<bool>(vm,"sphere"),"Sphere policy selected");
        for(int side=0;side<2;side++)
        {
            Require(Get<bool>(cursors[side],"poseValid") && !Get<bool>(cursors[side],"clicksAllowed"),"Sphere mode keeps accepted avatar sample and disabled clicks");
            Vector3 raw=Get<Vector3>(cursors[side],"rawPosition"),shown=Get<Vector3>(cursors[side],"position");
            Require(Vector3.Distance(raw,shown)/Mathf.Max(1,raw.magnitude)<.000002f,"Sphere mode starts at current raw point within float precision");
        }
        SetHands(0,1,Quaternion.Euler(0,90,0));
        for(int frame=0;frame<8;frame++) yield return null;
        for(int side=0;side<2;side++)
        {
            Vector3 root=Get<Vector3>(cursors[side],"handRoot");
            Require(Vector3.Angle(Get<Vector3>(cursors[side],"position")-root,Get<Vector3>(cursors[side],"rawPosition")-root)<9,"Wrist turn reaches direction in post-IK frames");
        }
        SetHands(90,1,Quaternion.identity);yield return null;
        for(int side=0;side<2;side++)
        {
            Require(Vector3.Distance(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"))<4,"Ordinary curl returns to working volume");
            RecordTemporal("sphere_return",side);
        }
        SetHands(0,1,Quaternion.identity);yield return null;SetHands(230,1,Quaternion.identity);yield return null;
        for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"),0,"Sphere full fist exactly at hand");
        SetHands(90,1,Quaternion.identity);yield return null;
        inputs[0].SetProgramVariable("lastSampleTime",Time.realtimeSinceStartup-1);yield return null;
        Near(Get<Vector3>(cursors[0],"position"),Get<Vector3>(cursors[0],"rawPosition"),.00001f,"Sphere gap recovery seeds raw");
        cursors[0].SetProgramVariable("smoothing",false);yield return null;Require(!Get<bool>(a,"valid"),"Sphere bypass cancels policy");
        cursors[0].SetProgramVariable("smoothing",true);yield return null;
        Near(Get<Vector3>(cursors[0],"position"),Get<Vector3>(cursors[0],"rawPosition"),.00001f,"Sphere resume seeds fresh");
        vm.SendCustomEvent("SetSphere");Require(Get<bool>(cursors[0],"poseValid"),"Idempotent sphere request preserves accepted sample");
        float multiplier=Get<float>(cursors[0],"rangeDistanceMultiplier");
        cursors[0].SetProgramVariable("rangeDistanceMultiplier",multiplier*.9f);cursors[0].SendCustomEvent("Step");
        Near(Get<Vector3>(cursors[0],"position"),Get<Vector3>(cursors[0],"rawPosition"),.00001f,"Range calibration change resets sphere history");
        cursors[0].SetProgramVariable("rangeDistanceMultiplier",multiplier);
        cursors[0].SetProgramVariable("adaptiveFilter",VM(control.adaptiveFilters[0]));cursors[0].SendCustomEvent("Step");
        Require(!Get<bool>(cursors[0],"poseValid"),"Conflicting policies rejected");
        cursors[0].SetProgramVariable("adaptiveFilter",null);yield return null;
        Require(Get<bool>(cursors[0],"poseValid"),"Recover after conflicting policy removed");
        Capture("bird-sphere-control",new Vector3(0,1.65f,-3),new Vector3(0,1.4f,1));
        vm.SendCustomEvent("SetRaw");yield return null;
    }
}
#endif
