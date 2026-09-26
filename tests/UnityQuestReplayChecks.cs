#if BIRD_OPENXR_ENABLED && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Bird3DCursor;
using UnityEngine;

// A private optional recording stays outside Assets and out of the APK/Git.
public static class UnityQuestReplayChecks
{
    static readonly int[] Indices = { 1,2,3,4,8,9,10,11,12,13,14,15,16,17,18,19 };
    sealed class RecordedHand : Hand
    {
        public Vector3[] joints;
        public bool tracked;
        public RecordedHand(Chirality chirality) : base(chirality) { }
        public override bool IsTracking() { return tracked; }
        public override Vector3 GetBasePosition(Finger f) { return joints[(int)f*4]; }
        public override Vector3 GetIntermediatePosition(Finger f) { return joints[(int)f*4+1]; }
        public override Vector3 GetDistalPosition(Finger f) { return joints[(int)f*4+2]; }
        public override Vector3 GetTipPosition(Finger f) { return joints[(int)f*4+3]; }
    }
    public static string RunOptional()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-birdJointTrace");
        if (flag < 0) return "SKIP: optional private joint replay (no -birdJointTrace supplied).";
        if (flag+1 >= args.Length || !File.Exists(args[flag+1])) throw new Exception("Joint trace argument does not exist");
        var go = new GameObject("Recorded hand replay");
        try
        {
            var cursor = go.AddComponent<BirdCursorState>(); cursor.fitter = go.AddComponent<BirdSphereFit>();
            cursor.useHandLimits = true; cursor.smoothing = false; cursor.clicksAllowed = false;
            var hand = new RecordedHand(Hand.Chirality.Right); var legacy = new Bird(hand);
            int rows=0, accepted=0, flat=0, ordinary=0, transforms=0;
            float oldError=0, transformError=0, oldAngle=0, newAngle=0;
            var legacyTilts = new List<float>();
            foreach (string line in File.ReadLines(args[flag+1]))
            {
                var row = JsonUtility.FromJson<UnityQuestHands.JointTrace>(line); rows++;
                if (!row.tracked) { cursor.Cancel(); continue; }
                if (row.schema != 1 || row.joints == null || row.joints.Length != 20) throw new Exception("Unsupported trace schema");
                var points = new Vector3[16]; for (int i=0;i<16;i++) points[i]=row.joints[Indices[i]];
                cursor.points=points; cursor.handRoot=.6f*row.joints[4]+.4f*row.joints[0]; cursor.indexTip=row.joints[7];
                cursor.palmNormal=row.palmNormal; cursor.tracking=true; cursor.flatDirectionDegrees=0;
                cursor.Step();
                if (!cursor.poseValid) throw new Exception("Recorded valid hand rejected at row " + rows);
                Vector3 baseline=cursor.rangeInput;
                if (row.appVersion == "0.5")
                {
                    oldError=Mathf.Max(oldError,Vector3.Distance(baseline,row.rangeInput));
                    if (oldError > .0001f) throw new Exception("Zero tilt fails to reproduce v0.5 input: " + oldError);
                }
                cursor.flatDirectionDegrees=45; cursor.Step(); accepted++;
                if (!cursor.poseValid) throw new Exception("Tilt invalidated recorded hand");
                Vector3 current=cursor.rangeInput;
                if (cursor.limitWeight == 0)
                {
                    ordinary++;
                    if (Vector3.Distance(current,baseline) > .000001f) throw new Exception("Tilt changed ordinary geometry");
                }
                hand.joints=row.joints; hand.tracked=true; legacy.Update();
                if (cursor.flatWeight == 1 && current.magnitude > .001f)
                {
                    flat++;
                    Vector3 n=row.palmNormal.normalized;
                    Vector3 f=Vector3.Cross(n,row.joints[16]-row.joints[4]).normalized;
                    if (Vector3.Dot(f,row.joints[8]-row.joints[0])<0) f=-f;
                    Vector3 reference=(legacy.GetSphereFitCenter()-cursor.handRoot).normalized;
                    legacyTilts.Add(Mathf.Atan2(Vector3.Dot(reference,f),Vector3.Dot(reference,n))*Mathf.Rad2Deg);
                    oldAngle+=Vector3.Angle(reference,baseline); newAngle+=Vector3.Angle(reference,current);
                    if (Mathf.Abs(Vector3.Angle(current,baseline)-45) > .02f || Mathf.Abs(current.magnitude-baseline.magnitude) > .0001f)
                        throw new Exception("Flat tilt changed distance or failed its hand-frame angle");
                }
                if (rows%30 == 0)
                {
                    Quaternion rotation=Quaternion.Euler(137,29,180); Vector3 offset=new Vector3(.2f,-.3f,.4f);
                    for (int i=0;i<16;i++) points[i]=rotation*points[i]+offset;
                    cursor.handRoot=rotation*cursor.handRoot+offset; cursor.indexTip=rotation*cursor.indexTip+offset;
                    cursor.palmNormal=rotation*row.palmNormal; cursor.Step(); transforms++;
                    transformError=Mathf.Max(transformError,Vector3.Distance(cursor.rangeInput,rotation*current));
                    if (!cursor.poseValid || transformError>.0002f) throw new Exception("Recorded upside-down/rigid replay mismatch: "+transformError);
                }
            }
            if (accepted==0 || flat==0 || ordinary==0) throw new Exception("Recording lacks usable flat/ordinary samples");
            legacyTilts.Sort();
            string result=string.Format(CultureInfo.InvariantCulture,
                "PASS: private replay rows={0}, valid={1}, ordinary={2}, fully-flat={3}; v0.5 input reproduction max={4:G6}m; hand-frame rotated cases={5}, max error={6:G6}m; flat legacy tilt median={7:F2}deg; mean direction error to legacy {8:F2}deg (normal) -> {9:F2}deg (45deg knuckle tilt). Recorded inputs, not physical v0.6 validation.",
                rows,accepted,ordinary,flat,oldError,transforms,transformError,legacyTilts[legacyTilts.Count/2],oldAngle/flat,newAngle/flat);
            File.WriteAllText("joint-replay-result.txt",result); return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
#endif
