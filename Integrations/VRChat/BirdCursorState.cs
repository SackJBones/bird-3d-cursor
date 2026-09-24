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
    public Transform cursorVisual;
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
        if (!fitter.fitValid) { Reject(); return; }
        Vector3 pointing = fitter.center - handRoot;
        float distance = pointing.magnitude;
        if (!Finite(distance) || distance <= 0) { Reject(); return; }
        // Preserve Bird.cs's unfiltered range law: x + x^2/.02 + .02*(x/.03)^6.
        float near = distance / 0.02f;
        float far = distance / 0.03f;
        float range = (near + near * near + far * far * far * far * far * far) * 0.02f;
        Vector3 candidate = handRoot + pointing / distance * range;
        if (!FiniteVector(candidate)) { Reject(); return; }
        Vector3 raw = candidate;
        float nextVariance = 1;
        if (smoothing && filterReady)
        {
            // Bird's scalar-covariance Vector3 Kalman recurrence: Q=.001, R=270*d^3.
            float noise = 270f * distance * distance * distance;
            float predicted = variance + 0.001f;
            float denominator = predicted + noise;
            if (!Finite(noise) || !Finite(denominator) || denominator <= 0) { Reject(); return; }
            float gain = predicted / denominator;
            nextVariance = noise * predicted / denominator;
            candidate = position + (candidate - position) * gain;
            if (!FiniteVector(candidate) || !Finite(nextVariance)) { Reject(); return; }
        }
        Vector3 selectCenter = (indexTip - fitter.center).sqrMagnitude < (indexTip - candidate).sqrMagnitude ? fitter.center : candidate;
        float depth = fitter.radius - (indexTip - selectCenter).magnitude;
        if (!Finite(depth)) { Reject(); return; }
        position = candidate;
        rawPosition = raw;
        variance = nextVariance;
        filterReady = smoothing;
        poseValid = true;
        if (!clicksAllowed) { up = selected; selected = false; }
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
