#if BIRD_OPENXR_ENABLED && UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Measures temporal behavior, including known undesirable lag. Never treats
// finite/reference-correct filtering as proof that distant return feels good.
public static class UnityQuestTemporalChecks
{
    static readonly int[] Indices={1,2,3,4,8,9,10,11,12,13,14,15,16,17,18,19};
    static readonly CultureInfo C=CultureInfo.InvariantCulture;
    sealed class FilterReference
    {
        public bool ready;
        double p=1, x,y,z;
        public double maxRelativeError;
        public void Check(BirdCursorState cursor)
        {
            Vector3 raw=cursor.rawPosition;
            if (!ready) { x=raw.x; y=raw.y; z=raw.z; p=1; ready=true; }
            else
            {
                double d=cursor.rangeInput.magnitude;
                double r=270*d*d*d, predicted=p+.001, k=predicted/(predicted+r);
                p=r*predicted/(predicted+r);
                x=x*(1-k)+raw.x*k; y=y*(1-k)+raw.y*k; z=z*(1-k)+raw.z*k;
            }
            double dx=cursor.position.x-x,dy=cursor.position.y-y,dz=cursor.position.z-z;
            double error=Math.Sqrt(dx*dx+dy*dy+dz*dz)/Math.Max(1,Math.Sqrt(x*x+y*y+z*z));
            maxRelativeError=Math.Max(maxRelativeError,error);
            if (error>0.00002) throw new Exception("Independent double Kalman reference mismatch: "+error);
        }
    }
    sealed class ReturnMetrics
    {
        public int samples, nearSamples, nearButFarSamples, returns, settled, censored;
        public float maxFilteredWhileNear, maxSettledDelay;
        bool havePrevious, previousNear, active;
        float start, startFiltered;
        readonly string name;
        readonly StringBuilder events;
        public ReturnMetrics(string name,StringBuilder events) { this.name=name; this.events=events; }
        public void Lose(float time) { End(time,false); havePrevious=false; }
        public void Add(float time,Vector3 root,Vector3 raw,Vector3 filtered)
        {
            float rawRange=Vector3.Distance(raw,root), filteredRange=Vector3.Distance(filtered,root);
            if (float.IsNaN(rawRange+filteredRange) || float.IsInfinity(rawRange+filteredRange)) throw new Exception("Nonfinite temporal sample");
            bool near=rawRange<4; samples++;
            if (near)
            {
                nearSamples++; maxFilteredWhileNear=Mathf.Max(maxFilteredWhileNear,filteredRange);
                if (filteredRange>=4) nearButFarSamples++;
            }
            if (!near) End(time,false);
            if (near && havePrevious && !previousNear)
            { active=true; returns++; start=time; startFiltered=filteredRange; }
            if (active && filteredRange<4) End(time,true);
            previousNear=near; havePrevious=true;
        }
        public void End(float time,bool reached)
        {
            if (!active) return;
            if (reached) { settled++; maxSettledDelay=Mathf.Max(maxSettledDelay,time-start); } else censored++;
            events.AppendFormat(C,"{0},{1:F6},{2:F6},{3:G9},{4}\n",name,start,time-start,startFiltered,reached ? "settled" : "left-near-or-ended");
            active=false;
        }
        public string Report()
        {
            return string.Format(C,"{0}: samples={1}, near={2}, near-but-filter-far={3}, max-filtered-while-raw-under-4m={4:G9}m, returns={5}, settled={6}, censored={7}, max-settled-delay={8:F6}s",
                name,samples,nearSamples,nearButFarSamples,maxFilteredWhileNear,returns,settled,censored,maxSettledDelay);
        }
    }
    public static void Run()
    {
        File.WriteAllText("joint-temporal-result.txt","PENDING");
        var go=new GameObject("Per-hand temporal replay audit");
        try
        {
            string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-birdJointTrace");
            if (index<0 || index+1>=args.Length || !File.Exists(args[index+1])) throw new Exception("Provide an existing -birdJointTrace file");
            var cursors=new BirdCursorState[4]; var refs=new FilterReference[4];
            var events=new StringBuilder("series,start_s,elapsed_s,initial_filtered_range_m,outcome\n");
            var metrics=new ReturnMetrics[6];
            for (int i=0;i<4;i++)
            {
                var cursor=go.AddComponent<BirdCursorState>(); cursor.fitter=go.AddComponent<BirdSphereFit>();
                cursor.useHandLimits=true; cursor.smoothing=true; cursor.clicksAllowed=false;
                cursor.flatDirectionDegrees=i/2==0 ? 0 : 45; cursors[i]=cursor; refs[i]=new FilterReference();
                metrics[i]=new ReturnMetrics((i/2==0 ? "replay0-" : "replay45-")+(i%2==0 ? "left" : "right"),events);
            }
            metrics[4]=new ReturnMetrics("observed-left",events); metrics[5]=new ReturnMetrics("observed-right",events);
            float[] last={-1,-1}; bool[] aligned={false,false}; int rows=0, alignedSamples=0, independenceChecks=0;
            float maxObservedRelativeError=0;
            foreach (string line in File.ReadLines(args[index+1]))
            {
                var row=JsonUtility.FromJson<UnityQuestHands.JointTrace>(line); rows++;
                if (row.schema!=1 || (row.hand!="Left" && row.hand!="Right") || row.joints==null || row.joints.Length!=20 || !float.IsFinite(row.time))
                    throw new Exception("Invalid recorded row schema");
                int side=row.hand=="Left" ? 0 : 1;
                if (row.time<=last[side]) throw new Exception("Timestamps must increase independently per hand");
                last[side]=row.time;
                if (row.tracked && row.poseValid) metrics[4+side].Add(row.time,row.root,row.raw,row.filtered);
                else metrics[4+side].Lose(row.time);
                for (int variant=0;variant<2;variant++)
                {
                    int slot=variant*2+side, other=variant*2+1-side;
                    Vector3 heldOther=cursors[other].position;
                    var cursor=cursors[slot];
                    if (!row.tracked)
                    {
                        cursor.Cancel(); refs[slot].ready=false; metrics[slot].Lose(row.time);
                        if (variant==0) aligned[side]=false;
                    }
                    else
                    {
                        var points=new Vector3[16]; for(int i=0;i<16;i++) points[i]=row.joints[Indices[i]];
                        cursor.points=points; cursor.handRoot=.6f*row.joints[4]+.4f*row.joints[0];
                        cursor.indexTip=row.joints[7]; cursor.palmNormal=row.palmNormal; cursor.tracking=true; cursor.Step();
                        if (!cursor.poseValid) throw new Exception("Recorded tracked input rejected at row "+rows);
                        refs[slot].Check(cursor);
                        metrics[slot].Add(row.time,cursor.handRoot,cursor.rawPosition,cursor.position);
                        if (variant==0 && row.appVersion=="0.5")
                        {
                            // A full fist sets both position=root and P=0. Before
                            // that point the recording's filter history is unknown.
                            if (row.fistWeight>=1 && cursor.fistWeight>=1) aligned[side]=true;
                            if (aligned[side])
                            {
                                alignedSamples++;
                                float error=Vector3.Distance(cursor.position,row.filtered)/Mathf.Max(1,row.filtered.magnitude);
                                maxObservedRelativeError=Mathf.Max(maxObservedRelativeError,error);
                                if (error>.0002f) throw new Exception("Post-fist v0.5 observed filter disagreement: "+error);
                            }
                        }
                    }
                    if (cursors[other].position!=heldOther) throw new Exception("One hand advanced the other hand's filter");
                    independenceChecks++;
                }
            }
            var report=new StringBuilder("MEASURED: per-hand temporal replay; known distant-history carryover remains.\n");
            report.AppendFormat(C,"rows={0}; hand-independence checks={1}; observed v0.5 samples after known fist reset={2}, max relative filter error={3:G9}\n",rows,independenceChecks,alignedSamples,maxObservedRelativeError);
            for(int i=0;i<6;i++) { metrics[i].End(last[i%2],false); report.AppendLine(metrics[i].Report()); }
            for(int i=0;i<4;i++) report.AppendFormat(C,"filter-reference-{0}: max relative error={1:G9}\n",i,refs[i].maxRelativeError);
            if (rows==0 || metrics[5].samples==0) throw new Exception("No valid right-hand temporal data");
            report.AppendLine("References and isolation checks PASS. Lag metrics are measurements, not usability passes. No runtime or installed APK change.");
            Directory.CreateDirectory("JointTemporalChecks");
            File.WriteAllText("JointTemporalChecks/near-returns.csv",events.ToString());
            File.WriteAllText("joint-temporal-result.txt",report.ToString());
            UnityEngine.Object.DestroyImmediate(go); EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            File.WriteAllText("joint-temporal-result.txt","FAIL: "+e);
            UnityEngine.Object.DestroyImmediate(go); EditorApplication.Exit(1);
        }
    }
}
#endif
