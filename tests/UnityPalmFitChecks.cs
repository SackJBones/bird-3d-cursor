#if UNITY_EDITOR
using System;
using UnityEngine;

// Shared by actual Unity C# and compiled Udon. Articulated synthetic joints,
// not human recordings: catches the flat/fist ambiguity in the old grid tests.
public static class UnityPalmFitChecks
{
    static int checks;
    public static string Check(Action<string, object> set, Func<string, object> get, Action<string> call)
    {
        checks = 0;
        Vector3 root = new Vector3(0, -.02f, 0);
        set("tracking", true); set("smoothing", false); set("clicksAllowed", false);
        set("rangeDistanceMultiplier", 1f); set("maximumLimitDistance", 2f);
        set("palmNormal", Vector3.forward); set("handRoot", root); set("indexTip", Vector3.up);
        Action<Vector3[]> sample = points => { set("points", points); call("Step"); Require((bool)get("poseValid"), "valid articulated pose"); };
        float ordinaryError = 0;
        foreach (float bend in new[] { 50f, 70, 90, 110, 130, 140 })
        {
            var points = Hand(bend);
            set("useHandLimits", false); sample(points); Vector3 expected = (Vector3)get("rawPosition");
            set("useHandLimits", true); sample(points);
            ordinaryError = Mathf.Max(ordinaryError, Vector3.Distance(expected, (Vector3)get("rawPosition")));
            Require((float)get("flatWeight") == 0 && (float)get("fistWeight") < .000001f, "ordinary pose uses legacy law " + bend);
            Require(ordinaryError < .00001f, "ordinary legacy parity at shipped 2m endpoint");
        }
        float maxStep = 0, maxLogStep = 0, maxFistRange = 0;
        Vector3 previous = Vector3.zero; float previousRange = 0;
        for (int i = 0; i <= 2400; i++)
        {
            float bend = 230 - i*.1f;
            sample(Hand(bend));
            Vector3 input = (Vector3)get("rangeInput"), raw = (Vector3)get("rawPosition");
            float range = Vector3.Distance(raw, root);
            Require(Finite(raw) && input.magnitude <= 2.001f, "finite two-law sweep");
            if (bend >= 211) { maxFistRange = Mathf.Max(maxFistRange, range); Require(raw == root, "closed fist exactly at root"); }
            if (i > 0)
            {
                maxStep = Mathf.Max(maxStep, Vector3.Distance(previous, input));
                maxLogStep = Mathf.Max(maxLogStep, Mathf.Abs(Mathf.Log(1+range)-Mathf.Log(1+previousRange)));
            }
            previous = input; previousRange = range;
        }
        Require(previousRange > 1e9f, "flat and overextended hand retains billion-meter logical reach");
        Require(maxStep < .07f && maxLogStep < .5f, "no discontinuity in 0.1 degree sweep: " + maxStep + ", " + maxLogStep);
        // A planar folded chain is maximally bent, despite a singular sphere.
        sample(Hand(230, 0, true));
        Require((Vector3)get("rawPosition") == root, "folded planar fist stays home");
        foreach (float proximalFraction in new[] { .38f, .40f, .42f, .46f })
        {
            sample(Hand(230, 0, false, proximalFraction));
            Require((Vector3)get("rawPosition") == root, "MCP across 90 degrees remains a fist");
        }
        float maxTransformError = 0;
        foreach (float scale in new[] { .7f, 1f, 1.3f })
        foreach (float bend in new[] { 230f, 210, 180, 140, 90, 45, 30, 15, 5, 0, -5 })
        {
            var source = Hand(bend, .00002f);
            for (int i = 0; i < source.Length; i++) source[i] *= scale;
            set("handRoot", root*scale); set("palmNormal", Vector3.forward); sample(source);
            Vector3 expectedInput = (Vector3)get("rangeInput");
            Quaternion rotation = Quaternion.Euler(23, -31, 41); Vector3 offset = new Vector3(.2f, -.3f, .4f);
            for (int i = 0; i < source.Length; i++) { source[i].x = -source[i].x; source[i] = rotation*source[i]+offset; }
            set("handRoot", rotation*(root*scale)+offset); set("palmNormal", rotation*Vector3.forward); sample(source);
            expectedInput.x = -expectedInput.x;
            float error = Vector3.Distance((Vector3)get("rangeInput"), rotation*expectedInput);
            maxTransformError = Mathf.Max(maxTransformError, error);
            Require(error < .0001f, "mirror/rigid transform " + bend + " error=" + error);
        }
        set("handRoot", root); set("palmNormal", Vector3.forward);
        sample(Hand(90)); Vector3 withoutIndex = (Vector3)get("rawPosition");
        set("indexTip", Vector3.one*20); sample(Hand(90));
        Require((Vector3)get("rawPosition") == withoutIndex, "index click independent of pose classifier");
        call("Cancel"); set("smoothing", true); sample(Hand(0));
        Require(Vector3.Distance((Vector3)get("position"), root) > 1e9f, "seed distant filter history");
        sample(Hand(230)); Require((Vector3)get("position") == root, "filtered fist returns exactly from billion-meter history");
        set("palmNormal", Vector3.zero); call("Step"); Require(!(bool)get("poseValid"), "invalid normal rejected");
        set("palmNormal", Vector3.forward); set("maximumLimitDistance", float.NaN); call("Step"); Require(!(bool)get("poseValid"), "invalid endpoint rejected");
        set("maximumLimitDistance", 2f); var bad = Hand(90); bad[7].x = float.NaN; set("points", bad); call("Step"); Require(!(bool)get("poseValid"), "invalid joint rejected");
        bad = Hand(90); bad[5] = bad[4]; set("points", bad); call("Step"); Require(!(bool)get("poseValid"), "collapsed bone rejected");
        sample(Hand(90)); Require((Vector3)get("position") == (Vector3)get("rawPosition"), "recovery seeds current point");
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0} hand-limit assertions; 2401 articulated samples; ordinary error={1:G6}m; max input step={2:G6}m/0.1deg, log(1+range) step={3:G6}; closed range={4:G6}m; mirror/rigid input error={5:G6}m. Synthetic hands, physical feel pending.",
            checks, ordinaryError, maxStep, maxLogStep, maxFistRange, maxTransformError);
    }
    static Vector3[] Hand(float bend, float noise = 0, bool foldedPlane = false, float proximalFraction = .32f)
    {
        var p = new Vector3[16];
        p[0] = new Vector3(-.043f, -.025f, .006f); p[1] = new Vector3(-.047f, -.008f, .012f); p[2] = new Vector3(-.05f, .01f, .016f);
        p[3] = new Vector3(-.025f, 0, 0);
        for (int f = 0; f < 3; f++)
        {
            int j = 4+f*4; p[j] = new Vector3((f-1)*.021f, -f*.004f, 0);
            float splay = (f-1)*.12f;
            float[] angles = { bend*proximalFraction, bend*.72f, bend };
            float[] lengths = { .038f-f*.003f, .025f-f*.002f, .021f-f*.002f };
            for (int k = 0; k < 3; k++)
            {
                float angle = angles[k]*Mathf.Deg2Rad;
                Vector3 segment = new Vector3(Mathf.Sin(splay)*Mathf.Cos(angle), Mathf.Cos(splay)*Mathf.Cos(angle), Mathf.Sin(angle));
                if (foldedPlane) segment = k == 0 ? Vector3.up : Vector3.down;
                p[j+k+1] = p[j+k]+segment*lengths[k];
            }
        }
        if (foldedPlane) { p[0].z = p[1].z = p[2].z = 0; }
        for (int i = 0; i < p.Length; i++) p[i] += new Vector3(Mathf.Sin(i*1.7f), Mathf.Cos(i*.7f), Mathf.Sin(i*.3f))*noise;
        return p;
    }
    static bool Finite(Vector3 v) { return !float.IsNaN(v.x+v.y+v.z) && !float.IsInfinity(v.x+v.y+v.z); }
    static void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
}
#endif
