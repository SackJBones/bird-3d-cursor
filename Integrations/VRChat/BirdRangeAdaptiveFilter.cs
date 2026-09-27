using UdonSharp;
using UnityEngine;

// Experimental caller-stepped policy. One instance per cursor; no geometry or rendering ownership.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdRangeAdaptiveFilter : UdonSharpBehaviour
{
    [Min(.01f)] public float workingRadius=4;
    [Min(.02f)] public float fullResponseRange=20;
    [Min(.005f)] public float farResponseSeconds=.05f;
    [Min(0)] public float returnMargin=4;
    [Min(.01f)] public float returnContractionRate=72;
    [HideInInspector] public Vector3 sampleRoot,samplePosition;
    [HideInInspector] public float sampleNoise, sampleDeltaTime=1f/72;
    [HideInInspector] public bool sampleFist;
    [HideInInspector] public Vector3 position;
    [HideInInspector] public float variance=1, gain, influence;
    [HideInInspector] public bool valid;
    [HideInInspector] public int historyRevision;
    private bool ready;
    private float boundWorking,boundFull,boundResponse,boundMargin,boundRate;

    public void Step()
    {
        valid=false;
        if(!enabled || !gameObject.activeInHierarchy || !Settings() || !FiniteVector(sampleRoot) || !FiniteVector(samplePosition) ||
            !Finite(sampleNoise) || sampleNoise<0 || !Finite(sampleDeltaTime) || sampleDeltaTime<=0 || sampleDeltaTime>.25f)
        { Cancel(); return; }
        float rawRange=(samplePosition-sampleRoot).magnitude;
        if(!Finite(rawRange)) { Cancel(); return; }
        if(workingRadius!=boundWorking || fullResponseRange!=boundFull || farResponseSeconds!=boundResponse ||
            returnMargin!=boundMargin || returnContractionRate!=boundRate)
        {
            Cancel(); boundWorking=workingRadius; boundFull=fullResponseRange; boundResponse=farResponseSeconds;
            boundMargin=returnMargin; boundRate=returnContractionRate;
        }
        float t=Mathf.Clamp01(Mathf.Log(Mathf.Max(rawRange,workingRadius)/workingRadius)/Mathf.Log(fullResponseRange/workingRadius));
        float weight=t*t*(3-2*t);
        if(!ready || sampleFist)
        {
            position=sampleFist?sampleRoot:samplePosition; variance=1; gain=1;
            influence=sampleFist?0:weight; ready=valid=true; return;
        }
        influence=Mathf.Max(influence,weight);
        float predicted=variance+.001f;
        // k >= 1-exp(-dt*w/tau) iff P >= R*(exp(dt*w/tau)-1).
        // Range is always the incoming hand-relative point, never delayed output.
        float x=sampleDeltaTime*weight/farResponseSeconds;
        float excess=x<.001f?x*(1+x*.5f+x*x/6):Mathf.Exp(x)-1;
        predicted=Mathf.Max(predicted,sampleNoise*excess);
        float denominator=predicted+sampleNoise;
        if(!Finite(denominator) || denominator<=0) { Cancel(); return; }
        gain=predicted/denominator;
        // Preserve the original float recurrence exactly in near-only histories.
        float nextVariance=sampleNoise*predicted/denominator;
        Vector3 history=position;
        if(influence>0)
        {
            Vector3 offset=history-sampleRoot;
            float length=offset.magnitude, start=rawRange+returnMargin;
            if(!Finite(length) || !Finite(start)) { Cancel(); return; }
            if(length>start)
            {
                float e=length-start;
                // C1 identity join; integrates de/dt=-rate*e^2 at full influence.
                // Only obsolete filter history is contracted. Raw reach is unchanged.
                float contracted=start+e/(1+e*sampleDeltaTime*returnContractionRate);
                Vector3 corrected=sampleRoot+offset*(contracted/length);
                history=history*(1-influence)+corrected*influence;
            }
        }
        Vector3 candidate=history*(1-gain)+samplePosition*gain;
        if(!FiniteVector(candidate) || !Finite(nextVariance)) { Cancel(); return; }
        position=candidate; variance=nextVariance; ready=valid=true;
        if((position-sampleRoot).magnitude<=workingRadius) influence=0;
    }
    public void Cancel()
    {
        historyRevision=historyRevision==int.MaxValue?0:historyRevision+1;
        ready=valid=false; influence=0; gain=0; variance=1;
    }
    private void OnDisable() { Cancel(); }
    private bool Settings()
    {
        return Finite(workingRadius) && workingRadius>=.01f && Finite(fullResponseRange) && fullResponseRange>workingRadius &&
            Finite(farResponseSeconds) && farResponseSeconds>=.005f && Finite(returnMargin) && returnMargin>=workingRadius &&
            Finite(returnContractionRate) && returnContractionRate>0;
    }
    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
