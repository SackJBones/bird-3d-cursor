#if UNITY_EDITOR
using System;
using UnityEngine;

// The same cases can target ordinary C# or compiled Udon through delegates.
public static class UnityPalmFitChecks
{
    public static string Check(Action<Vector3[], Vector3, Vector3, float, bool> fit,
        Func<bool> valid, Func<Vector3> center, Func<float> radius)
    {
        int samples = 0;
        float maxStep = 0, maxMirror = 0, maxRigid = 0;
        Vector3 previous = Vector3.zero;
        float previousRange = 0, maxRangeStep = 0;
        // Cupped -> perfectly flat -> hyperextended -> cupped, with small
        // deterministic non-spherical perturbations and no temporal smoothing.
        for (int frame = 0; frame <= 1600; frame++)
        {
            float curvature = frame <= 800 ? 60 - frame*.1f : -20 + (frame - 800)*.1f;
            Vector3[] points = Patch(curvature, false);
            fit(points, Vector3.zero, Vector3.forward, .08f, true);
            Require(valid(), "Continuous patch rejected at " + frame);
            Vector3 c = center();
            float r = radius();
            Require(c.z >= -.000001f && c.magnitude <= .080001f && r > 0 && !float.IsNaN(r), "Front/bounded/finite fit");
            float range = Range(c.magnitude);
            if (frame > 0)
            {
                maxStep = Mathf.Max(maxStep, (c - previous).magnitude);
                maxRangeStep = Mathf.Max(maxRangeStep, Mathf.Abs(range - previousRange));
            }
            previous = c;
            previousRange = range;
            if (frame % 40 == 0)
            {
                for (int j = 0; j < points.Length; j++) points[j].x = -points[j].x;
                fit(points, Vector3.zero, Vector3.forward, .08f, true);
                Require(valid(), "Mirrored hand rejected");
                maxMirror = Mathf.Max(maxMirror, (center() - new Vector3(-c.x, c.y, c.z)).magnitude);
                var rotation = Quaternion.Euler(frame*.2f, 127, -43);
                Vector3 translation = new Vector3(.3f, 1.2f, -.4f);
                points = Patch(curvature, false);
                for (int j = 0; j < points.Length; j++) points[j] = rotation*points[j] + translation;
                fit(points, translation, rotation*Vector3.forward, .08f, true);
                Require(valid(), "Rigidly transformed hand rejected");
                maxRigid = Mathf.Max(maxRigid, (center() - (rotation*c + translation)).magnitude);
            }
            samples++;
        }
        Require(maxStep < .001f, "Center jumped >1mm for a .1/m curvature increment: " + maxStep);
        Require(maxRangeStep < .15f, "Raw polynomial output discontinuity: " + maxRangeStep);
        Require(maxMirror < .00001f && maxRigid < .00003f, "Mirroring/rigid transform changed fit");

        // The shipped 2m numerical sphere endpoint means ~1.76e9m of logical
        // Bird reach, not the .08m-center stress fixture's short range.
        Vector3 priorFar = Vector3.zero;
        float maxFarStep=0;
        for(int frame=0;frame<=800;frame++)
        {
            fit(Patch(2-frame*.005f,true),Vector3.zero,Vector3.forward,2,true);
            Require(valid() && center().z>=0 && center().magnitude<=2.00001f,"Long-reach continuation failed");
            if(frame>0)maxFarStep=Mathf.Max(maxFarStep,(center()-priorFar).magnitude);
            priorFar=center();
        }
        Require(maxFarStep<.03f,"Long-range center discontinuity: "+maxFarStep);
        Require(Range(priorFar.magnitude)>1e9f,"Numerical endpoint truncated long reach");

        // Exact planar and symmetric curved caps, including infinitesimal
        // perturbations around zero, do not need the previous valid frame.
        Vector3 flat = Vector3.zero;
        foreach (float k in new[] { 0f, .0001f, -.0001f, 0f })
        {
            fit(Patch(k, true), Vector3.zero, Vector3.forward, .08f, true);
            Require(valid() && center().z > 0, "Fresh flat/inverted cap must produce front-facing fit");
            if (k == 0) flat = center();
            Require((center() - flat).magnitude < .00001f, "Discontinuity at perfectly flat cap");
        }
        // Fully curved sphere where the continuation is inactive retains the
        // original least-squares result (including radius recomputation).
        var sphere = new Vector3[16];
        for (int j = 0; j < 16; j++)
        {
            float y = 1 - 2*(j+.5f)/16;
            float a = j*2.39996323f, xz = Mathf.Sqrt(1-y*y);
            sphere[j] = new Vector3(0, 0, .025f) + new Vector3(xz*Mathf.Cos(a), y, xz*Mathf.Sin(a))*.02f;
        }
        fit(sphere, Vector3.zero, Vector3.forward, .08f, false);
        var legacy = center(); float legacyRadius = radius();
        fit(sphere, Vector3.zero, Vector3.forward, .08f, true);
        Require(valid() && (center() - legacy).magnitude < .000001f && Mathf.Abs(radius() - legacyRadius) < .000001f, "Ordinary sphere changed");
        fit(Patch(0, true), Vector3.zero, Vector3.zero, .08f, true);
        Require(!valid() && center() == Vector3.zero && radius() == 0, "Missing normal must clear output");
        fit(Patch(0, true), Vector3.zero, Vector3.forward, float.NaN, true);
        Require(!valid(), "Nonfinite cap accepted");
        var invalid = Patch(0, true); invalid[2].z = float.NaN;
        fit(invalid, Vector3.zero, Vector3.forward, .08f, true);
        Require(!valid(), "Nonfinite joint accepted");
        fit(Patch(0, true), Vector3.zero, Vector3.forward, .08f, true);
        Require(valid(), "Recovery failed");
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "PASS: {0} continuous palm samples + 801 billion-meter-reach samples + mirrored/rigid/flat/legacy/invalid/recovery checks; max center step={1:F4}mm raw range step={2:F4}m mirror={3:F5}mm rigid={4:F5}mm",
            samples, maxStep*1000, maxRangeStep, maxMirror*1000, maxRigid*1000);
    }

    static Vector3[] Patch(float curvature, bool exact)
    {
        var points = new Vector3[16];
        for (int j = 0; j < 16; j++)
        {
            float x = (j%4 - 1.5f)*.015f + .003f;
            float y = (j/4 - 1.5f)*.018f + .006f;
            float z = .5f*curvature*(x*x + y*y);
            if (!exact) z += .00002f*Mathf.Sin(j*2.1f);
            points[j] = new Vector3(x, y, z);
        }
        return points;
    }
    static float Range(float d) { float f = d/.03f; return d + d*d/.02f + .02f*f*f*f*f*f*f; }
    static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
}
#endif
