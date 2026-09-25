#if BIRD_OPENXR_ENABLED
using System;
using System.Reflection;
using Bird3DCursor;
using UnityEngine;

// Preflight runs with real Unity vectors/matrices and the exact production math
// copied by the runner. This C# comparison complements the separate Udon VM tests.
public static class UnityQuestHandsChecks
{
    sealed class SampleHand : Hand
    {
        public readonly Vector3[] points = new Vector3[16];
        public Vector3 root, tip;
        public bool tracked = true;
        public SampleHand() : base(Chirality.Left) { }
        public override bool IsTracking() { return tracked; }
        public override Vector3 GetBasePosition(Finger f) { return f == Finger.Thumb ? (root - .6f * points[3]) / .4f : f == Finger.Index ? points[3] : points[4 + ((int)f - 2) * 4]; }
        public override Vector3 GetIntermediatePosition(Finger f) { return f == Finger.Thumb ? points[0] : f == Finger.Index ? tip : points[5 + ((int)f - 2) * 4]; }
        public override Vector3 GetDistalPosition(Finger f) { return f == Finger.Thumb ? points[1] : f == Finger.Index ? tip : points[6 + ((int)f - 2) * 4]; }
        public override Vector3 GetTipPosition(Finger f) { return f == Finger.Thumb ? points[2] : f == Finger.Index ? tip : points[7 + ((int)f - 2) * 4]; }
    }

    public static string Run()
    {
        var go = new GameObject("Math preflight");
        try
        {
            var port = go.AddComponent<BirdCursorState>();
            port.fitter = go.AddComponent<BirdSphereFit>();
            port.smoothing = true;
            var hand = new SampleHand();
            var bird = new Bird(hand);
            var curve = typeof(Bird).GetMethod("birdRangeFunc", BindingFlags.Instance | BindingFlags.NonPublic);
            if (curve == null) throw new Exception("Original range function not found");
            var originalFilter = new KalmanFilterVector3(.001f, .06f);
            var portFilter = new KalmanFilterVector3(.001f, .06f);
            float maxFit = 0, maxRaw = 0, maxFilter = 0;
            for (int frame = 0; frame < 400; frame++)
            {
                float t = frame * .03f;
                Vector3 center = new Vector3(.2f + .03f * Mathf.Sin(t), 1.2f, .5f + .05f * Mathf.Cos(t));
                float radius = .025f + .006f * Mathf.Sin(t * .8f);
                var rotation = Quaternion.Euler(23 + t * 7, 31 + t * 9, 42);
                for (int j = 0; j < 16; j++)
                {
                    float y = 1 - 2 * (j + .5f) / 16;
                    float r = Mathf.Sqrt(1 - y * y), angle = j * 2.39996323f;
                    hand.points[j] = center + rotation * new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle)) *
                        (radius + .00003f * Mathf.Sin(frame * .7f + j));
                }
                float d = .012f + .053f * (.5f + .5f * Mathf.Sin(t));
                hand.root = center - rotation * Vector3.forward * d;
                hand.tip = center + rotation * Vector3.up * (radius + .03f);
                bird.Update();
                port.points = hand.points;
                port.handRoot = bird.GetHandRoot();
                port.indexTip = hand.tip;
                port.tracking = true;
                port.Step();
                if (!port.poseValid) throw new Exception("Port rejected valid sphere at " + frame);
                var pointing = bird.GetSphereFitCenter() - bird.GetHandRoot();
                float actualD = pointing.magnitude;
                float originalRange = (float)curve.Invoke(bird, new object[] { actualD });
                if (originalRange != UnityQuestHands.Range(actualD)) throw new Exception("Diagnostic range differs from original");
                Vector3 raw = bird.GetHandRoot() + pointing / actualD * originalRange;
                float fitGap = Vector3.Distance(port.fitter.center, bird.GetSphereFitCenter());
                float rawGap = Vector3.Distance(raw, port.rawPosition);
                maxFit = Mathf.Max(maxFit, fitGap);
                maxRaw = Mathf.Max(maxRaw, rawGap);
                if (fitGap > .00001f || Mathf.Abs(port.fitter.radius - bird.GetSphereFitRadius()) > .00001f || rawGap > .001f)
                    throw new Exception("Fit/range mismatch at " + frame + ": " + fitGap + ", " + rawGap);
                Vector3 expectedOriginal = originalFilter.Update(raw, null, actualD * actualD * actualD * 270);
                if (Vector3.Distance(expectedOriginal, bird.GetPosition()) > .00001f) throw new Exception("Original Kalman recurrence mismatch");
                float portD = (port.fitter.center - port.handRoot).magnitude;
                Vector3 expectedPort;
                if (frame == 0) { portFilter.Reset(port.rawPosition); expectedPort = port.rawPosition; }
                else expectedPort = portFilter.Update(port.rawPosition, null, 270 * portD * portD * portD);
                maxFilter = Mathf.Max(maxFilter, Vector3.Distance(expectedPort, port.position));
                if (maxFilter > .00001f) throw new Exception("Port Kalman recurrence mismatch");
            }
            port.Cancel();
            port.Step();
            if (!port.poseValid || Vector3.Distance(port.position, port.rawPosition) > .000001f)
                throw new Exception("Port must seed first valid measurement after loss");
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "PASS: 400 moving noisy sphere samples; original/port center max={0:F6}mm raw max={1:F6}mm; port Kalman reference max={2:F6}mm; recovery seed verified", maxFit * 1000, maxRaw * 1000, maxFilter * 1000);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
#endif
