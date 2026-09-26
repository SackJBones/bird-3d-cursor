using UdonSharp;
using UnityEngine;

// Caller-fed cursor state with optional smoothing. One fitter per cursor; no avatar mapping/networking.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdCursorState : UdonSharpBehaviour
{
    public BirdSphereFit fitter;
    public Vector3[] points;
    public Vector3 handRoot;
    public Vector3 indexTip;
    public bool tracking;
    public bool smoothing;
    public bool clicksAllowed = true;
    public float rangeDistanceMultiplier = 1;
    public Transform cursorVisual;
    // Optional geometric limit law. Requires Bird's canonical 16 fit points and
    // a caller-verified normal facing OUT of the palm. No rendering assumptions.
    public bool useHandLimits;
    public Vector3 palmNormal;
    public float flatBlendStartDegrees = 45;
    public float flatBlendEndDegrees = 15;
    public float fistBlendStartDegrees = 140;
    public float fistBlendEndDegrees = 210;
    // Endpoint of the range function's INPUT, not a cursor range cap.
    public float maximumLimitDistance = 2;
    [HideInInspector] public float bendDegrees;
    [HideInInspector] public float flatWeight;
    [HideInInspector] public float fistWeight;
    [HideInInspector] public float limitWeight;
    [HideInInspector] public Vector3 rangeInput;
    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 rawPosition;
    [HideInInspector] public bool poseValid;
    [HideInInspector] public bool selected;
    [HideInInspector] public bool down;
    [HideInInspector] public bool up;
    private bool filterReady;
    private float variance = 1;

    // Pulses describe this sample, not a Unity frame. Consumers read after each Step.
    public void Step()
    {
        down = up = false;
        if (!tracking || fitter == null || !FiniteVector(handRoot) || !FiniteVector(indexTip)) { Reject(); return; }
        fitter.points = points;
        fitter.Fit();
        Vector3 pointing = fitter.center - handRoot;
        flatWeight = fistWeight = limitWeight = 0;
        if (useHandLimits)
        {
            if (!HandLimits()) { Reject(); return; }
            pointing = rangeInput;
        }
        else if (!fitter.fitValid) { Reject(); return; }
        rangeInput = pointing;
        float distance = pointing.magnitude;
        if (!Finite(distance) || (!useHandLimits && distance <= 0)) { Reject(); return; }
        if (!Finite(rangeDistanceMultiplier) || rangeDistanceMultiplier <= 0) { Reject(); return; }
        float mappedDistance = distance * rangeDistanceMultiplier;
        if (!Finite(mappedDistance)) { Reject(); return; }
        // Preserve Bird.cs's unfiltered range law: x + x^2/.02 + .02*(x/.03)^6.
        float near = mappedDistance / 0.02f;
        float far = mappedDistance / 0.03f;
        float range = (near + near * near + far * far * far * far * far * far) * 0.02f;
        Vector3 candidate = distance > 0 ? handRoot + pointing / distance * range : handRoot;
        if (!FiniteVector(candidate)) { Reject(); return; }
        Vector3 raw = candidate;
        float nextVariance = 1;
        if (smoothing && filterReady)
        {
            // Bird's scalar-covariance Vector3 Kalman recurrence: Q=.001, R=270*d^3.
            float noise = 270f * mappedDistance * mappedDistance * mappedDistance;
            float predicted = variance + 0.001f;
            float denominator = predicted + noise;
            if (!Finite(noise) || !Finite(denominator) || denominator <= 0) { Reject(); return; }
            float gain = predicted / denominator;
            nextVariance = noise * predicted / denominator;
            // Weighted form avoids catastrophic cancellation on a long return.
            // The explicit fist endpoint also clears distant filter history.
            candidate = fistWeight >= 1 ? handRoot : position * (1 - gain) + candidate * gain;
            if (!FiniteVector(candidate) || !Finite(nextVariance)) { Reject(); return; }
        }
        Vector3 selectCenter = (indexTip - fitter.center).sqrMagnitude < (indexTip - candidate).sqrMagnitude ? fitter.center : candidate;
        float depth = fitter.fitValid ? fitter.radius - (indexTip - selectCenter).magnitude : 0;
        if (!Finite(depth)) { Reject(); return; }
        position = candidate;
        rawPosition = raw;
        variance = nextVariance;
        filterReady = smoothing;
        poseValid = true;
        if (!clicksAllowed || !fitter.fitValid || fistWeight > 0) { up = selected; selected = false; }
        else
        {
            if (!selected && depth > 0.007f) { selected = true; down = true; }
            if (selected && depth < 0.005f) { selected = false; up = true; }
        }
        if (cursorVisual != null)
        {
            cursorVisual.position = position;
            cursorVisual.gameObject.SetActive(true);
        }
    }

    private bool HandLimits()
    {
        if (points == null || points.Length != 16 || !FiniteVector(palmNormal) || palmNormal.sqrMagnitude < 0.000000000001f ||
            !Finite(maximumLimitDistance) || maximumLimitDistance <= 0 ||
            !Finite(flatBlendEndDegrees) || !Finite(flatBlendStartDegrees) || !Finite(fistBlendStartDegrees) || !Finite(fistBlendEndDegrees) ||
            flatBlendEndDegrees < 0 || flatBlendStartDegrees <= flatBlendEndDegrees ||
            fistBlendStartDegrees <= flatBlendStartDegrees || fistBlendEndDegrees <= fistBlendStartDegrees) return false;
        for (int i = 0; i < 16; i++) if (!FiniteVector(points[i])) return false;
        Vector3 normal = palmNormal.normalized;
        // Recover the thumb base from Bird's canonical weighted root. A stable
        // palm axis avoids reversing the curl sign when an MCP crosses 90 deg.
        Vector3 thumbBase = (handRoot - .6f*points[3]) / .4f;
        Vector3 forward = (points[3]+points[4]+points[8]+points[12])*.25f - thumbBase;
        forward -= normal*Vector3.Dot(forward, normal);
        if (forward.sqrMagnitude < 0.000000000001f) return false;
        Vector3 flexAxis = Vector3.Cross(forward, normal);
        float bend = 0, length = 0, reachRatio = 0;
        // Middle/ring/little only: moving the index to click cannot change this
        // pose classifier. Straight finger splay also leaves bend unchanged.
        for (int i = 4; i < 16; i += 4)
        {
            Vector3 a = points[i+1] - points[i], b = points[i+2] - points[i+1], c = points[i+3] - points[i+2];
            float la = a.magnitude, lb = b.magnitude, lc = c.magnitude;
            if (la < 0.000001f || lb < 0.000001f || lc < 0.000001f) return false;
            a /= la; b /= lb; c /= lc;
            // Signed MCP elevation allows an extended hand to flare backward;
            // finger joint flexion distinguishes a folded fist from a plane.
            float height = Mathf.Clamp(Vector3.Dot(a, normal), -1, 1);
            float tangent = Mathf.Sqrt(Mathf.Max(0, 1-height*height));
            if (Vector3.Dot(a, forward) < 0) tangent = -tangent;
            float elevation = Mathf.Atan2(height, tangent) * Mathf.Rad2Deg;
            bend += Mathf.Max(0, elevation + Vector3.SignedAngle(a, b, flexAxis) + Vector3.SignedAngle(b, c, flexAxis));
            length += la + lb + lc;
            reachRatio += (points[i+3]-points[i]).magnitude / (la+lb+lc);
        }
        bendDegrees = bend / 3;
        length /= 3;
        flatWeight = 1 - Smooth((bendDegrees-flatBlendEndDegrees) / (flatBlendStartDegrees-flatBlendEndDegrees));
        fistWeight = Mathf.Max(Smooth((bendDegrees-fistBlendStartDegrees) / (fistBlendEndDegrees-fistBlendStartDegrees)),
            Smooth((0.55f-reachRatio/3) / 0.2f));
        // Independent, finite palm-outward law. Its reciprocal bend grows only
        // toward extension; closing NEVER selects the far endpoint. Squared
        // bend has zero slope at flat, so slight backward flare stays continuous.
        float halfLength = length * 0.5f;
        float angle = bendDegrees * Mathf.Deg2Rad;
        float fallbackDistance = halfLength / Mathf.Sqrt(angle*angle + halfLength*halfLength / (maximumLimitDistance*maximumLimitDistance));
        Vector3 fallback = normal * fallbackDistance;
        float legacyWeight = fitter.fitValid ? (1-flatWeight) * fitter.confidence : 0;
        limitWeight = 1 - legacyWeight;
        // Blend before the original polynomial: blending billion-meter outputs
        // directly would pull even a tiny blend weight out of the working volume.
        Vector3 legacy = fitter.fitValid ? fitter.center-handRoot : Vector3.zero;
        rangeInput = (legacy*legacyWeight + fallback*(1-legacyWeight)) * (1-fistWeight);
        return FiniteVector(rangeInput);
    }

    private float Smooth(float value) { float t = Mathf.Clamp01(value); return t*t*(3 - 2*t); }

    public void Cancel()
    {
        down = up = false;
        Reject();
    }

    private void Reject()
    {
        poseValid = false;
        filterReady = false;
        up = selected;
        selected = false;
        if (cursorVisual != null) cursorVisual.gameObject.SetActive(false);
        // Hold last finite position; poseValid prevents it being mistaken for a new sample.
    }
    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
