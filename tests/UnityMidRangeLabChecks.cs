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
    // Compare settings through the compiled center + output filters. No runtime
    // default is changed by this experiment, and no new openness signal is used.
    void MidRangeContracts(UdonBehaviour center, UdonBehaviour output)
    {
        var rows = new List<string> { "full_effect_m,hz,target_m,case,metric,value,unit" };
        float savedFull = Get<float>(center, "fullEffectRange");
        Action<float,int,float,string,string,double,string> record = (full,hz,range,scenario,metric,value,unit) =>
            rows.Add(string.Format(CultureInfo.InvariantCulture, "{0:R},{1},{2:R},{3},{4},{5:R},{6}", full,hz,range,scenario,metric,value,unit));
        try
        {
            foreach (float full in new[] { 20f, 12f, 8f })
            foreach (int hz in new[] { 30, 72, 120 })
            foreach (float range in new[] { 3f, 4f, 6f, 8f, 12f, 20f, 50f })
            {
                center.SetProgramVariable("fullEffectRange", full);
                float lo=0, hi=1;
                for (int i=0; i<32; i++) { float mid=(lo+hi)*.5f; if (SphereReach(mid)<range) lo=mid; else hi=mid; }
                float length=(lo+hi)*.5f, dt=1f/hz;
                Vector3 neutral=Vector3.forward*length;
                // Independent center noise and deliberately correlated radial
                // noise distinguish tremor attenuation from just delaying intent.
                foreach (string scenario in new[] { "white", "correlated" })
                {
                    center.SendCustomEvent("Cancel"); output.SendCustomEvent("Cancel");
                    for (int i=0; i<hz; i++) CenterChain(center,output,neutral,Vector3.zero,dt);
                    var random=new System.Random(42061);
                    double squared=0, rawSquared=0, bias=0, angularSquared=0;
                    var jumps=new List<float>(); float previous=range;
                    int n=hz*6;
                    for (int i=0; i<n; i++)
                    {
                        var noise=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*length*.04f;
                        if (scenario=="correlated") noise.z+=length*.02f*Mathf.Sin(2*Mathf.PI*2*i*dt);
                        Vector3 raw=neutral+noise;
                        Vector3 point=CenterChain(center,output,raw,Vector3.zero,dt);
                        float shown=point.magnitude, error=shown-range, rawError=SphereReach(raw.magnitude)-range;
                        squared+=error*error; rawSquared+=rawError*rawError; bias+=error;
                        float angle=Vector3.Angle(point,neutral); angularSquared+=angle*angle;
                        jumps.Add(Mathf.Abs(shown-previous)); previous=shown;
                    }
                    jumps.Sort();
                    record(full,hz,range,scenario,"range_rms",Math.Sqrt(squared/n),"metres");
                    record(full,hz,range,scenario,"raw_range_rms",Math.Sqrt(rawSquared/n),"metres");
                    record(full,hz,range,scenario,"range_bias",bias/n,"metres");
                    record(full,hz,range,scenario,"range_jump_p95",jumps[(int)(.95f*(n-1))],"metres_per_sample");
                    record(full,hz,range,scenario,"angular_rms",Math.Sqrt(angularSquared/n),"degrees");
                }
                foreach (float angle in new[] { 15f, 90f })
                {
                    center.SendCustomEvent("Cancel"); output.SendCustomEvent("Cancel");
                    for (int i=0; i<hz; i++) CenterChain(center,output,neutral,Vector3.zero,dt);
                    Vector3 target=Quaternion.AngleAxis(angle,Vector3.up)*neutral;
                    float arrival=-1, minimum=1;
                    for (int i=0; i<hz*2; i++)
                    {
                        Vector3 point=CenterChain(center,output,target,Vector3.zero,dt);
                        minimum=Mathf.Min(minimum,point.magnitude/range);
                        if (arrival<0 && Vector3.Angle(point,target)<angle*.1f) arrival=(i+1f)/hz;
                    }
                    Require(arrival>0,"Mid-range direction step settles");
                    record(full,hz,range,"turn_"+angle,"arrival_90pct",arrival,"seconds");
                    record(full,hz,range,"turn_"+angle,"minimum_range_fraction",minimum,"ratio");
                }
                foreach (float factor in new[] { .5f, 2f })
                {
                    center.SendCustomEvent("Cancel"); output.SendCustomEvent("Cancel");
                    for (int i=0; i<hz; i++) CenterChain(center,output,neutral,Vector3.zero,dt);
                    lo=0; hi=1;
                    for (int i=0; i<32; i++) { float mid=(lo+hi)*.5f; if (SphereReach(mid)<range*factor) lo=mid; else hi=mid; }
                    Vector3 target=Vector3.forward*((lo+hi)*.5f);
                    float arrival=-1;
                    for (int i=0; i<hz*2; i++)
                    {
                        float shown=CenterChain(center,output,target,Vector3.zero,dt).magnitude;
                        if (arrival<0 && Mathf.Abs(shown-range*factor)<=Mathf.Abs(range-range*factor)*.1f) arrival=(i+1f)/hz;
                    }
                    Require(arrival>0,"Mid-range radial step settles");
                    record(full,hz,range,factor<1?"close_half":"open_double","arrival_90pct",arrival,"seconds");
                }
            }
        }
        finally
        {
            center.SetProgramVariable("fullEffectRange",savedFull);
            center.SendCustomEvent("Cancel"); output.SendCustomEvent("Cancel");
            Directory.CreateDirectory("../Validation/TrackingLab");
            File.WriteAllLines("../Validation/TrackingLab/mid-range.csv",rows);
        }
    }

    IEnumerator MidRangeBoneScenarios()
    {
        var rows=new List<string>{"target_m,side,full_effect_m,bend_deg,ideal_m,radial_rms_m,angular_rms_deg,raw_radial_rms_m,singular_samples"};
        float[] saved={Get<float>(centerPolicies[0],"fullEffectRange"),Get<float>(centerPolicies[1],"fullEffectRange")};
        try
        {
            SetHands(0,1,Quaternion.identity); yield return null;
            for(int side=0;side<2;side++)
            {
                inputs[side].SendCustomEvent("CalibrateOpenHand");
                cursors[side].SetProgramVariable("centerFilter",centerPolicies[side]);
                centerPolicies[side].SetProgramVariable("fullEffectRange",side==0?20f:8f);
            }
            foreach(float target in new[]{3f,6f,8f,12f,20f})
            {
                // Find the actual articulated pose through the avatar adapter
                // and fitter; never force its multiplier or its computed point.
                float lo=45,hi=110,bend=90;
                for(int i=0;i<16;i++)
                {
                    bend=(lo+hi)*.5f; SetHands(bend,1,Quaternion.identity); yield return null;
                    if(Range(0)>target) lo=bend; else hi=bend;
                }
                Require(Mathf.Abs(Range(0)-target)<.01f,"Articulated mid-range fixture hits requested range");
                for(int i=0;i<36;i++) { SetHands(bend,1,Quaternion.identity); yield return null; }
                Vector3[] ideal=new Vector3[2];
                for(int side=0;side<2;side++) ideal[side]=Get<Vector3>(cursors[side],"rawPosition")-Get<Vector3>(cursors[side],"handRoot");
                var random=new System.Random(84172);
                double[] radial=new double[2],angular=new double[2],rawRadial=new double[2];
                int[] invalid=new int[2]; const int n=180;
                for(int frame=0;frame<n;frame++)
                {
                    SetHands(bend,1,Quaternion.identity);
                    for(int bone=1;bone<16;bone++)
                    {
                        Vector3 noise=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.0006f;
                        positions[Bone(0,bone)]+=noise; positions[Bone(1,bone)]+=CenterReflect(noise,true);
                    }
                    yield return null;
                    for(int side=0;side<2;side++)
                    {
                        Require(Get<bool>(cursors[side],"poseValid"),"Noisy room-scale avatar pose remains valid");
                        Vector3 root=Get<Vector3>(cursors[side],"handRoot");
                        Vector3 point=Get<Vector3>(cursors[side],"position")-root;
                        float dr=point.magnitude-ideal[side].magnitude;
                        float raw=(Get<Vector3>(cursors[side],"rawPosition")-root).magnitude-ideal[side].magnitude;
                        float angle=Vector3.Angle(point,ideal[side]);
                        radial[side]+=dr*dr; angular[side]+=angle*angle; rawRadial[side]+=raw*raw;
                        if(!Get<bool>(fitters[side],"fitValid")) invalid[side]++;
                    }
                }
                for(int side=0;side<2;side++) rows.Add(string.Format(CultureInfo.InvariantCulture,"{0:R},{1},{2},{3:R},{4:R},{5:R},{6:R},{7:R},{8}",
                    target,side,side==0?20:8,bend,ideal[side].magnitude,Math.Sqrt(radial[side]/n),Math.Sqrt(angular[side]/n),Math.Sqrt(rawRadial[side]/n),invalid[side]));
            }
        }
        finally
        {
            for(int side=0;side<2;side++) { centerPolicies[side].SetProgramVariable("fullEffectRange",saved[side]); centerPolicies[side].SendCustomEvent("Cancel"); }
            Directory.CreateDirectory("../Validation/TrackingLab");
            File.WriteAllLines("../Validation/TrackingLab/mid-range-bones.csv",rows);
        }
    }
}
#endif
