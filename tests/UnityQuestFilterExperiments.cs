#if BIRD_OPENXR_ENABLED && UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Editor-only comparison candidates, NOT shipped Bird filtering policy.
public static class UnityQuestFilterExperiments
{
    static readonly CultureInfo C=CultureInfo.InvariantCulture;
    static readonly int[] Indices={1,2,3,4,8,9,10,11,12,13,14,15,16,17,18,19};
    static readonly string[] Names={"legacy", "log-radial", "elastic-history"};
    sealed class Filter
    {
        public Vector3 position;
        public bool ready;
        float limitInfluence;
        float variance=1;
        readonly int mode;
        public Filter(int mode) { this.mode=mode; }
        public Vector3 Step(Vector3 root,Vector3 raw,float input,bool fist=false,float deltaTime=1f/72,float flatWeight=0)
        {
            if (!ready) { ready=true; limitInfluence=flatWeight; variance=1; return position=raw; }
            limitInfluence=Mathf.Max(limitInfluence,flatWeight);
            float r=270*input*input*input, p=variance+.001f, k=p/(p+r);
            variance=r*p/(p+r);
            Vector3 history=position;
            if (mode==1)
                position=root+Expand(Compress(history-root)*(1-k)+Compress(raw-root)*k);
            else
            {
                if (mode==2 && limitInfluence>0)
                {
                    Vector3 contracted=ContractHistory(history,root,(raw-root).magnitude,deltaTime);
                    history=history*(1-limitInfluence)+contracted*limitInfluence;
                }
                position=history*(1-k)+raw*k;
            }
            if (fist) position=root;
            if ((position-root).magnitude<=4) limitInfluence=0;
            Require(Finite(position), "finite candidate output");
            return position;
        }
        static Vector3 Compress(Vector3 v)
        {
            double r=v.magnitude;
            return r<=4 ? v : v*(float)((4+4*Math.Log(r/4))/r);
        }
        static Vector3 Expand(Vector3 v)
        {
            double r=v.magnitude;
            return r<=4 ? v : v*(float)(4*Math.Exp((r-4)/4)/r);
        }
    }
    static void Require(bool ok,string message) { if (!ok) throw new Exception(message); }
    static bool Finite(Vector3 v) { return float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z); }
    static Vector3 ContractHistory(Vector3 history,Vector3 root,float rawRange,float deltaTime)
    {
        Vector3 offset=history-root;
        float length=offset.magnitude, start=rawRange+4;
        if(length<=start || deltaTime<=0) return history;
        // Identity below raw radius + 4m, C1 at activation. Integrate
        // de/dt = -e^2/(width*tau), width=1m, tau=1/72s, for excess e.
        // Timestamp-scaled correction; the retained Q/R remains sample-based.
        // Does not bound raw geometry or a steady far target.
        float excess=length-start;
        return root+offset*((start+excess/(1+excess*deltaTime*72))/length);
    }
    static float InputForRange(float range)
    {
        // Independent monotonic inversion for synthetic raw-range trajectories.
        double lo=0,hi=2;
        for(int i=0;i<60;i++)
        {
            double m=(lo+hi)/2, result=m+m*m/.02+.02*Math.Pow(m/.03,6);
            if(result<range) lo=m; else hi=m;
        }
        return (float)((lo+hi)/2);
    }
    static Filter[] Filters() { return new[]{new Filter(0),new Filter(1),new Filter(2)}; }
    static UnityQuestTemporalChecks.ReturnMetrics[] Metrics(string name,StringBuilder events)
    {
        var result=new UnityQuestTemporalChecks.ReturnMetrics[3];
        for(int i=0;i<3;i++) result[i]=new UnityQuestTemporalChecks.ReturnMetrics(name+"-"+Names[i],events);
        return result;
    }
    static void Finish(UnityQuestTemporalChecks.ReturnMetrics[] metrics,float time,StringBuilder report)
    { foreach(var m in metrics) { m.End(time,false); report.AppendLine(m.Report()); } }
    static void Synthetic(StringBuilder report,StringBuilder events)
    {
        float maxOrdinaryError=0, maxRigidError=0, maxNonLimitError=0;
        float maxContractionError=0;
        foreach(float dt in new[]{1f/30,1f/72,1f/120,.017f})
        {
            Vector3 value=Vector3.forward*1e9f;
            float elapsed=0;
            while(elapsed<.2f)
            {
                float step=Mathf.Min(dt,.2f-elapsed);
                value=ContractHistory(value,Vector3.zero,.5f,step); elapsed+=step;
            }
            double expected=4.5+1/(1/(1e9-4.5)+.2*72);
            maxContractionError=Mathf.Max(maxContractionError,Mathf.Abs(value.z-(float)expected));
        }
        Require(maxContractionError<.00001f,"constant-target contraction partition reference");
        report.AppendFormat(C,"History contraction exact-flow max partition error={0:G9}m\n",maxContractionError);
        var noLimit=new Filter(2); var slightLimit=new Filter(2);
        noLimit.Step(Vector3.zero,Vector3.forward*1e6f,InputForRange(1e6f));
        slightLimit.Step(Vector3.zero,Vector3.forward*1e6f,InputForRange(1e6f));
        Vector3 unmodified=noLimit.Step(Vector3.zero,Vector3.forward*.5f,InputForRange(.5f));
        Vector3 slight=slightLimit.Step(Vector3.zero,Vector3.forward*.5f,InputForRange(.5f),false,1f/72,.000001f);
        float onsetChange=(slight-unmodified).magnitude/unmodified.magnitude;
        Require(onsetChange<.000002f,"infinitesimal flat contribution must not switch full correction on");
        report.AppendFormat(C,"At flatWeight=1e-6, relative correction onset={0:G9}\n",onsetChange);
        Quaternion rotation=Quaternion.Euler(173,31,-19);
        Vector3 shift=new Vector3(.3f,-.6f,.2f);
        foreach(int hz in new[]{30,72,120})
        {
            var filters=Filters(); var transformed=Filters();
            var nonLimit=new Filter(2);
            var ordinary=Filters(); var metrics=Metrics("synthetic"+hz,events);
            for(int sample=0;sample<=hz*5;sample++)
            {
                float time=(float)sample/hz;
                float exponent=time<1?0:time<2?time-1:time<3?1:time<3.2f?1-(time-3)/.2f:0;
                float range=.5f*Mathf.Exp(Mathf.Log(2e9f)*exponent);
                Vector3 root=new Vector3(.1f*Mathf.Sin(time),.05f*time,0);
                Vector3 direction=new Vector3(.15f*Mathf.Sin(time*2),.1f,1).normalized;
                Vector3 raw=root+direction*range;
                Vector3 mirrorRoot=new Vector3(-root.x,root.y,root.z), mirrorRaw=new Vector3(-raw.x,raw.y,raw.z);
                float input=InputForRange(range);
                float fromFlatLimit=time>=1.2f && time<3.2f ? 1 : 0;
                Vector3 unchanged=nonLimit.Step(root,raw,input,false,1f/hz,0);
                for(int mode=0;mode<3;mode++)
                {
                    Vector3 value=filters[mode].Step(root,raw,input,false,1f/hz,fromFlatLimit);
                    if(mode==0) maxNonLimitError=Mathf.Max(maxNonLimitError,(value-unchanged).magnitude/Mathf.Max(1,value.magnitude));
                    metrics[mode].Add(time,root,raw,value);
                    Vector3 rotated=transformed[mode].Step(rotation*mirrorRoot+shift,rotation*mirrorRaw+shift,input,false,1f/hz,fromFlatLimit);
                    Vector3 expected=rotation*new Vector3(-value.x,value.y,value.z)+shift;
                    float error=(rotated-expected).magnitude/Mathf.Max(1,expected.magnitude);
                    maxRigidError=Mathf.Max(maxRigidError,error);
                    Require(error<.00005f,"rigid/mirror filter error "+Names[mode]+": "+error);
                    if(sample==hz*2 || sample==hz*3)
                        report.AppendFormat(C,"synthetic{0}-{1}: t={2}s, raw={3:G9}m, filtered={4:G9}m\n",hz,Names[mode],time,range,(value-root).magnitude);
                    if(mode==2 && time<=3)
                        Require((value-filters[0].position).magnitude/Mathf.Max(1,value.magnitude)<.000001f,"elastic preserves synthetic outward trajectory");
                }
                // Entirely ordinary history: exact intended legacy behavior.
                float near=1+.4f*Mathf.Sin(time*5);
                for(int mode=0;mode<3;mode++) ordinary[mode].Step(root,root+direction*near,InputForRange(near),false,1f/hz);
                for(int mode=1;mode<3;mode++) maxOrdinaryError=Mathf.Max(maxOrdinaryError,(ordinary[mode].position-ordinary[0].position).magnitude);
            }
            Finish(metrics,5,report);
            foreach(var filter in filters)
            {
                Vector3 root=new Vector3(.2f,.3f,.4f);
                Require(filter.Step(root,root,0,true)==root,"fist endpoint");
                filter.ready=false;
                Vector3 recovery=root+Vector3.forward*100;
                Require(filter.Step(root,recovery,InputForRange(100))==recovery,"tracking recovery seed");
            }
        }
        Require(maxOrdinaryError<.00001f,"ordinary legacy equivalence");
        Require(maxNonLimitError<.000001f,"nonlimiting near/far history retains legacy filtering");
        report.AppendFormat(C,"Elastic nonlimiting whole-trajectory max relative error={0:G9}\n",maxNonLimitError);
        report.AppendFormat(C,"Synthetic ordinary max error={0:G9}m; rigid/mirror max relative error={1:G9}; fist and recovery PASS.\n",maxOrdinaryError,maxRigidError);
    }
    public static void Run()
    {
        File.WriteAllText("filter-experiments-result.txt","PENDING");
        var go=new GameObject("Editor-only filter policy experiments");
        try
        {
            var report=new StringBuilder("MEASURED: experimental filters, no runtime or APK change.\n");
            var events=new StringBuilder("series,start_s,elapsed_s,initial_filtered_range_m,outcome\n");
            Synthetic(report,events);
            string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-birdJointTrace");
            Require(index>=0 && index+1<args.Length && File.Exists(args[index+1]),"Existing private -birdJointTrace required");
            var cursors=new BirdCursorState[2]; var filters=new[]{Filters(),Filters()};
            var metrics=new[]{Metrics("record-left",events),Metrics("record-right",events)};
            for(int side=0;side<2;side++)
            {
                var cursor=go.AddComponent<BirdCursorState>(); cursor.fitter=go.AddComponent<BirdSphereFit>();
                cursor.useHandLimits=true; cursor.smoothing=true; cursor.clicksAllowed=false;
                cursor.flatDirectionDegrees=45; cursors[side]=cursor;
            }
            float[] last={-1,-1}; int valid=0; float maxBaselineError=0;
            foreach(string line in File.ReadLines(args[index+1]))
            {
                var row=JsonUtility.FromJson<UnityQuestHands.JointTrace>(line);
                Require(row.schema==1 && (row.hand=="Left"||row.hand=="Right") && row.joints!=null && row.joints.Length==20 && float.IsFinite(row.time),"trace schema");
                int side=row.hand=="Left"?0:1;
                Require(row.time>last[side],"per-hand increasing time");
                float deltaTime=last[side]<0 ? 0 : row.time-last[side]; last[side]=row.time;
                var cursor=cursors[side];
                if(!row.tracked)
                {
                    cursor.Cancel();
                    for(int mode=0;mode<3;mode++) { filters[side][mode].ready=false; metrics[side][mode].Lose(row.time); }
                    continue;
                }
                var points=new Vector3[16]; for(int i=0;i<16;i++) points[i]=row.joints[Indices[i]];
                cursor.points=points; cursor.handRoot=.6f*row.joints[4]+.4f*row.joints[0];
                cursor.indexTip=row.joints[7]; cursor.palmNormal=row.palmNormal; cursor.tracking=true; cursor.Step();
                Require(cursor.poseValid,"tracked pose valid"); valid++;
                for(int mode=0;mode<3;mode++)
                {
                    Vector3 value=filters[side][mode].Step(cursor.handRoot,cursor.rawPosition,cursor.rangeInput.magnitude,cursor.fistWeight>=1,deltaTime,cursor.flatWeight);
                    metrics[side][mode].Add(row.time,cursor.handRoot,cursor.rawPosition,value);
                    if(mode==0) maxBaselineError=Mathf.Max(maxBaselineError,(value-cursor.position).magnitude/Mathf.Max(1,cursor.position.magnitude));
                }
            }
            Require(valid>0 && maxBaselineError<.000001f,"baseline matches production");
            report.AppendFormat(C,"Recorded usable samples={0}, baseline max relative error={1:G9}\n",valid,maxBaselineError);
            for(int side=0;side<2;side++) Finish(metrics[side],last[side],report);
            report.AppendLine("No candidate is promoted by this measurement. Synthetic timing is not hardware timing or subjective feel.");
            Directory.CreateDirectory("JointTemporalChecks");
            File.WriteAllText("JointTemporalChecks/filter-experiment-returns.csv",events.ToString());
            File.WriteAllText("filter-experiments-result.txt",report.ToString());
            UnityEngine.Object.DestroyImmediate(go); EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            File.WriteAllText("filter-experiments-result.txt","FAIL: "+e);
            UnityEngine.Object.DestroyImmediate(go); EditorApplication.Exit(1);
        }
    }
}
#endif
