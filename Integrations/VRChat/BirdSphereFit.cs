using UdonSharp;
using UnityEngine;

// Algebraic least-squares sphere fit; input selection and missing-bone policy belong to the caller.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdSphereFit : UdonSharpBehaviour
{
    public Vector3[] points;
    [HideInInspector] public bool fitValid;
    [HideInInspector] public Vector3 center;
    [HideInInspector] public float radius;

    public void Fit()
    {
        fitValid = false;
        center = Vector3.zero;
        radius = 0;
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

    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
