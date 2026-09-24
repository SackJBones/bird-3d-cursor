using UdonSharp;
using UnityEngine;

// Caller-fed, unsmoothed cursor state. One fitter per cursor; no avatar mapping or networking.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdCursorState : UdonSharpBehaviour
{
    public BirdSphereFit fitter;
    public Vector3[] points;
    public Vector3 handRoot;
    public Vector3 indexTip;
    public bool tracking;
    public Transform cursorVisual;
    [HideInInspector] public Vector3 position;
    [HideInInspector] public bool poseValid;
    [HideInInspector] public bool selected;
    [HideInInspector] public bool down;
    [HideInInspector] public bool up;

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
        Vector3 selectCenter = (indexTip - fitter.center).sqrMagnitude < (indexTip - candidate).sqrMagnitude ? fitter.center : candidate;
        float depth = fitter.radius - (indexTip - selectCenter).magnitude;
        if (!Finite(depth)) { Reject(); return; }
        position = candidate;
        poseValid = true;
        if (!selected && depth > 0.007f) { selected = true; down = true; }
        if (selected && depth < 0.005f) { selected = false; up = true; }
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
        up = selected;
        selected = false;
        if (cursorVisual != null) cursorVisual.gameObject.SetActive(false);
        // Hold last finite position; poseValid prevents it being mistaken for a new sample.
    }
    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
