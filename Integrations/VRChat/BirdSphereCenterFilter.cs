using UdonSharp;
using UnityEngine;

// Optional pre-geometry Vector3 Kalman stage. State is center-minus-current-palm,
// in world axes and the range law's normalized units, never Euler angles.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdSphereCenterFilter : UdonSharpBehaviour
{
    [Tooltip("Process covariance per sample at 72 Hz, in normalized square metres.")]
    public float processNoise = .000001f;
    [Tooltip("Measurement covariance at 72 Hz, in normalized square metres.")]
    public float measurementNoise = .000009f;
    [Tooltip("Keep the accepted nearby behavior exactly; blend this extra stage in beyond this raw Bird range.")]
    public float workingRadius = 4;
    public float fullEffectRange = 20;
    [HideInInspector] public Vector3 sampleVector;
    [HideInInspector] public float sampleRange, sampleDeltaTime = 1f/72;
    [HideInInspector] public Vector3 vector;
    [HideInInspector] public float variance = 1, gain, influence;
    [HideInInspector] public bool valid;
    [HideInInspector] public int historyRevision;
    private Vector3 state;
    private bool ready;
    private float boundQ, boundR, boundWorking, boundFull;

    public void Step()
    {
        valid = false;
        if (!enabled || !gameObject.activeInHierarchy || !Positive(processNoise) || !Positive(measurementNoise) ||
            !Positive(workingRadius) || !Finite(fullEffectRange) || fullEffectRange <= workingRadius ||
            !FiniteVector(sampleVector) || !Finite(sampleRange) || sampleRange < 0 ||
            !Positive(sampleDeltaTime) || sampleDeltaTime > .25f)
        { Cancel(); return; }
        if (boundQ != processNoise || boundR != measurementNoise || boundWorking != workingRadius || boundFull != fullEffectRange)
        {
            Cancel(); boundQ = processNoise; boundR = measurementNoise;
            boundWorking = workingRadius; boundFull = fullEffectRange;
        }
        float t = Mathf.Clamp01(Mathf.Log(Mathf.Max(sampleRange, workingRadius) / workingRadius) / Mathf.Log(fullEffectRange / workingRadius));
        influence = t*t*(3-2*t);
        if (!ready || influence == 0)
        {
            // No distant history can delay a return to the working volume.
            state = vector = sampleVector; variance = 1; gain = 1; ready = valid = true; return;
        }
        // Same scalar-covariance Vector3 recurrence as KalmanFilterVector3.
        // Q scales with elapsed time; R inversely with elapsed time so the
        // steady response/noise tradeoff is comparable across frame rates.
        float step = sampleDeltaTime * 72;
        float q = processNoise * step, r = measurementNoise / step;
        float predicted = variance + q, denominator = predicted + r;
        if (!Positive(denominator)) { Cancel(); return; }
        gain = predicted / denominator;
        variance = r * predicted / denominator;
        state = state * (1-gain) + sampleVector * gain;
        vector = sampleVector * (1-influence) + state * influence;
        if (!FiniteVector(vector) || !Finite(variance)) { Cancel(); return; }
        ready = valid = true;
    }
    public void Cancel()
    {
        historyRevision = historyRevision == int.MaxValue ? 0 : historyRevision+1;
        ready = valid = false; variance = 1; gain = influence = 0;
    }
    private void OnDisable() { Cancel(); }
    private bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    private bool Positive(float x) { return Finite(x) && x > 0; }
    private bool FiniteVector(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
}
