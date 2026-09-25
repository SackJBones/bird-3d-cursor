using UdonSharp;
using UnityEngine;

// Algebraic least-squares sphere fit; input selection and missing-bone policy belong to the caller.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdSphereFit : UdonSharpBehaviour
{
    public Vector3[] points;
    // Opt-in: caller supplies a front-facing palm normal and the cursor root.
    // Generic/synthetic fits retain their original unconstrained behavior.
    public bool constrainToPalm;
    public Vector3 palmNormal;
    public Vector3 palmOrigin;
    // A numerical endpoint for the sphere center, NOT a near-workspace cursor
    // range cap: Bird's original polynomial maps 2m to about 1.76 billion m.
    public float maximumCenterDistance = 2f;
    [HideInInspector] public float continuationWeight;
    [HideInInspector] public bool fitValid;
    [HideInInspector] public Vector3 center;
    [HideInInspector] public float radius;
    private float unconstrainedConfidence;

    public void Fit()
    {
        FitUnconstrained();
        continuationWeight = 0;
        if (constrainToPalm) ContinueAtPalm();
    }

    private void FitUnconstrained()
    {
        fitValid = false;
        center = Vector3.zero;
        radius = 0;
        unconstrainedConfidence = 0;
        if (points == null || points.Length < 4 || points.Length > 32) return;
        Vector3 origin = points[0];
        if (!FiniteVector(origin)) return;
        Vector3 meanOffset = Vector3.zero;
        for (int i = 0; i < points.Length; i++)
        {
            if (!FiniteVector(points[i])) return;
            meanOffset += points[i] - origin;
        }
        meanOffset /= points.Length;
        float scale = 0;
        for (int i = 0; i < points.Length; i++)
            scale = Mathf.Max(scale, ((points[i] - origin) - meanOffset).magnitude);
        if (!Finite(scale) || scale <= 0) return;

        // Center and normalize before forming the normal equations. This preserves the
        // existing algebraic objective while making the singularity guard scale independent.
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        Vector3 rhs = Vector3.zero;
        float meanSquared = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = ((points[i] - origin) - meanOffset) / scale;
            float sq = p.sqrMagnitude;
            xx += p.x * p.x; xy += p.x * p.y; xz += p.x * p.z;
            yy += p.y * p.y; yz += p.y * p.z; zz += p.z * p.z;
            rhs += p * (0.5f * sq);
            meanSquared += sq;
        }
        float count = points.Length;
        xx /= count; xy /= count; xz /= count; yy /= count; yz /= count; zz /= count;
        rhs /= count;
        meanSquared /= count;
        float a = yy * zz - yz * yz;
        float b = xz * yz - xy * zz;
        float c = xy * yz - xz * yy;
        float d = xx * zz - xz * xz;
        float e = xy * xz - xx * yz;
        float f = xx * yy - xy * xy;
        float determinant = xx * a + xy * b + xz * c;
        unconstrainedConfidence = Smooth((determinant - 0.000001f) / 0.00001f);
        // Reject underdetermined/near-planar sets rather than emitting a huge unstable fit.
        if (!Finite(determinant) || determinant <= 0.000001f) return;
        Vector3 offset = new Vector3(a * rhs.x + b * rhs.y + c * rhs.z,
            b * rhs.x + d * rhs.y + e * rhs.z,
            c * rhs.x + e * rhs.y + f * rhs.z) / determinant;
        Vector3 candidateCenter = origin + meanOffset + offset * scale;
        float candidateRadius = Mathf.Sqrt(meanSquared + offset.sqrMagnitude) * scale;
        if (!FiniteVector(candidateCenter) || !Finite(candidateRadius) || candidateRadius <= 0) return;
        center = candidateCenter;
        radius = candidateRadius;
        fitValid = true;
    }

    private void ContinueAtPalm()
    {
        bool originalValid = fitValid;
        Vector3 originalCenter = center;
        fitValid = false;
        center = Vector3.zero;
        radius = 0;
        if (points == null || points.Length < 4 || points.Length > 32 || !FiniteVector(palmOrigin) ||
            !FiniteVector(palmNormal) || palmNormal.sqrMagnitude < 0.000000000001f ||
            !Finite(maximumCenterDistance) || maximumCenterDistance <= 0) return;
        Vector3 n = palmNormal.normalized;
        Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.8f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(n, u);
        Vector3 origin = points[0];
        Vector3 meanOffset = Vector3.zero;
        for (int i = 0; i < points.Length; i++)
        {
            if (!FiniteVector(points[i])) return;
            meanOffset += points[i] - origin;
        }
        meanOffset /= points.Length;
        Vector3 mean = origin + meanOffset;
        float scale = 0;
        for (int i = 0; i < points.Length; i++) scale = Mathf.Max(scale, ((points[i] - origin) - meanOffset).magnitude);
        if (!Finite(scale) || scale <= 0.000001f) return;

        // Regress palm height z on x, y and centered squared distance s.
        // Unlike solving for center height, the curvature coefficient remains
        // finite through a plane and becomes negative on the inverted side.
        float xx = 0, xy = 0, yy = 0, xz = 0, yz = 0;
        float xs = 0, ys = 0, zs = 0, ss = 0, meanSquared = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 q = ((points[i] - origin) - meanOffset) / scale;
            float x = Vector3.Dot(q, u), y = Vector3.Dot(q, v), z = Vector3.Dot(q, n), s = q.sqrMagnitude;
            xx += x*x; xy += x*y; yy += y*y; xz += x*z; yz += y*z;
            xs += x*s; ys += y*s; zs += z*s; ss += s*s; meanSquared += s;
        }
        float count = points.Length;
        xx /= count; xy /= count; yy /= count; xz /= count; yz /= count;
        xs /= count; ys /= count; zs /= count; ss /= count; meanSquared /= count;
        // Tiny dimensionless ridge makes circular/near-degenerate regression
        // inputs well-defined. Truly collapsed inputs/palm frames are rejected.
        xx += 0.000001f; yy += 0.000001f;
        float determinant = xx*yy - xy*xy;
        if (!Finite(determinant) || determinant <= 0) return;
        float sx = (yy*xs - xy*ys) / determinant;
        float sy = (xx*ys - xy*xs) / determinant;
        float zx = (yy*xz - xy*yz) / determinant;
        float zy = (xx*yz - xy*xz) / determinant;
        float residualSquared = Mathf.Max(0, ss - meanSquared*meanSquared - xs*sx - ys*sy);
        float alpha = (zs - xs*zx - ys*zy) / (residualSquared + 0.000001f);
        float beta = zx - alpha*sx, gamma = zy - alpha*sy;
        Vector3 axis = n - beta*u - gamma*v;
        float axisLength = axis.magnitude;
        Vector3 direction = axis / axisLength;
        float curvature = 2*alpha / (scale*axisLength);
        if (!Finite(curvature) || !FiniteVector(direction)) return;

        Vector3 fromRoot = mean - palmOrigin;
        float along = Vector3.Dot(fromRoot, direction);
        float discriminant = along*along + maximumCenterDistance*maximumCenterDistance - fromRoot.sqrMagnitude;
        if (!Finite(discriminant) || discriminant <= 0) return;
        float maxHeight = -along + Mathf.Sqrt(discriminant);
        float minHeight = Mathf.Max(0, -Vector3.Dot(fromRoot, n) / Vector3.Dot(direction, n));
        if (!Finite(maxHeight) || maxHeight <= 0 || minHeight > maxHeight) return;

        // C1 soft cap: unchanged reciprocal above the shoulder; zero slope at
        // zero curvature. Flat and backward curvature share the finite endpoint.
        float shoulder = maxHeight * (2f / 3f);
        float t = curvature * shoulder;
        float height = t >= 1 ? 1 / curvature : maxHeight * (1 - Mathf.Max(0, t)*Mathf.Max(0, t) / 3);
        height = Mathf.Max(minHeight, height);
        Vector3 continued = mean + direction*height;

        // Preserve the original objective for ordinary, well-curved poses.
        // Blend BEFORE singularity; both endpoints must be in the front half-ball.
        float originalWeight = 0;
        if (originalValid)
        {
            Vector3 delta = originalCenter - palmOrigin;
            originalWeight = unconstrainedConfidence * Smooth((curvature*maxHeight - 1.5f) / 0.5f) *
                Smooth(Vector3.Dot(delta, n) / (maximumCenterDistance*0.1f)) *
                (1 - Smooth((delta.magnitude / maximumCenterDistance - 0.8f) / 0.2f));
        }
        Vector3 candidate = Vector3.Lerp(continued, originalCenter, originalWeight);
        // Refitting the algebraic intercept gives r^2=mean|point-center|^2.
        float squareRadius = meanSquared*scale*scale + (candidate - mean).sqrMagnitude;
        if (!FiniteVector(candidate) || !Finite(squareRadius) || squareRadius <= 0) return;
        center = candidate;
        radius = Mathf.Sqrt(squareRadius);
        continuationWeight = 1 - originalWeight;
        fitValid = true;
    }

    private float Smooth(float value) { float t = Mathf.Clamp01(value); return t*t*(3 - 2*t); }

    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
