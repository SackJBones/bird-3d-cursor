using System;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

// Local Udon counterpart of Bird3DCursor.UI.BirdRangeScale. Own-pivot scale only.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(110)]
public class BirdUiRangeScale : UdonSharpBehaviour
{
    [Tooltip("Fixed collider outside the scaled target hierarchy.")]
    public Collider engagementVolume;
    public Transform scaleTarget;
    public BirdUiPointer[] pointers=new BirdUiPointer[0];
    public BirdUiPanel panel;
    [Tooltip("Positive multipliers around rest scale; interval must contain 1.")]
    public float minimumFactor=.25f,maximumFactor=4;
    [Tooltip("2 preserves legacy squared range. Response is log-scale inverse seconds; 0 is immediate.")]
    public float rangeExponent=2,response=12;
    public UdonBehaviour eventTarget;
    public string startedEvent,stoppedEvent,changedEvent;
    public bool automatic=true;
    [HideInInspector] public float stepDelta=1f/72,factor=1,desiredFactor=1;
    [HideInInspector] public bool scalingEnabled;
    [HideInInspector] public BirdUiPointer activePointer;
    private Transform bound,parent;
    private string owner;
    private Vector3 restScale,expectedScale;
    private Matrix4x4 volumeFrame,parentFrame;
    private double currentLog,desiredLog,previousDesired,anchorLogRange,anchorLogScale;
    private int revision;
    private bool stepping;
    public void Initialize() { if(stepping) return; Cancel(); bound=null; Bind(); }
    public void StartScaling() { if(enabled && gameObject.activeInHierarchy && ValidSettings() && Bind()) scalingEnabled=true; }
    public void StopScaling() { Cancel(); }
    [RecursiveMethod] public void Cancel() { scalingEnabled=false; EndGesture(); }
    public void ResetScale() { Cancel(); if(ValidSettings() && Bind()) Apply(0); }
    private void OnDisable() { Cancel(); }
    private void LateUpdate() { if(automatic) { stepDelta=Time.unscaledDeltaTime; Process(); } }
    private bool Bind()
    {
        if(scaleTarget==null || !scaleTarget.gameObject.activeInHierarchy) return false;
        if(bound==scaleTarget) return true;
        EndGesture(); bound=scaleTarget; restScale=expectedScale=bound.localScale;
        currentLog=desiredLog=previousDesired=0; factor=desiredFactor=1; return ValidScale(restScale);
    }
    private bool ValidSettings()
    {
        return Finite(minimumFactor) && minimumFactor>0 && minimumFactor<=1 && Finite(maximumFactor) && maximumFactor>=1 &&
            Finite(rangeExponent) && rangeExponent>0 && Finite(response) && response>=0;
    }
    private bool Eligible(BirdUiPointer pointer)
    {
        return pointer!=null && pointer.IsTracked() && (panel==null || panel.Accepts(pointer) && panel.state==1);
    }
    private bool Contact(BirdUiPointer pointer,out double range)
    {
        range=0;
        if(!Eligible(pointer) || engagementVolume==null || !engagementVolume.enabled || !engagementVolume.gameObject.activeInHierarchy || engagementVolume.transform.IsChildOf(scaleTarget)) return false;
        System.Type type=engagementVolume.GetType();
        if(type==typeof(MeshCollider)) { MeshCollider mesh=(MeshCollider)engagementVolume; if(!mesh.convex || mesh.sharedMesh==null) return false; }
        else if(type!=typeof(BoxCollider) && type!=typeof(SphereCollider) && type!=typeof(CapsuleCollider)) return false;
        Vector3 delta=pointer.position-pointer.origin;
        range=Math.Sqrt((double)delta.x*delta.x+(double)delta.y*delta.y+(double)delta.z*delta.z);
        if(range<=1e-5 || double.IsNaN(range) || double.IsInfinity(range)) return false;
        if((engagementVolume.ClosestPoint(pointer.origin)-pointer.origin).sqrMagnitude<1e-10f) return true;
        RaycastHit hit; return engagementVolume.Raycast(new Ray(pointer.origin,delta/(float)range),out hit,(float)range);
    }
    public void Process()
    {
        if(stepping || !enabled || !gameObject.activeInHierarchy || !scalingEnabled) return;
        stepping=true; Step(); stepping=false;
    }
    private void Step()
    {
        float dt=stepDelta;
        if(!Finite(dt) || dt<=0 || dt>.25f || !ValidSettings() || !Bind() || !ValidScale(restScale) || !ValidScale(scaleTarget.localScale) ||
            (scaleTarget.localScale-expectedScale).sqrMagnitude>1e-10f*expectedScale.sqrMagnitude) { Cancel(); return; }
        double range;
        if(activePointer!=null && (!Contact(activePointer,out range) || activePointer.userId!=owner || !activePointer.hasHistory && activePointer.revision!=revision ||
            scaleTarget.parent!=parent || ParentFrame()!=parentFrame || engagementVolume.transform.localToWorldMatrix!=volumeFrame)) { EndGesture(); return; }
        if(activePointer==null)
        {
            foreach(BirdUiPointer pointer in pointers)
            {
                if(!Contact(pointer,out range)) continue;
                activePointer=pointer; owner=pointer.userId; anchorLogRange=Math.Log(range); anchorLogScale=currentLog; revision=pointer.revision;
                parent=scaleTarget.parent; parentFrame=ParentFrame(); volumeFrame=engagementVolume.transform.localToWorldMatrix;
                previousDesired=desiredLog=currentLog; desiredFactor=factor;
                Notify(startedEvent); return;
            }
            return;
        }
        if(!Contact(activePointer,out range)) { EndGesture(); return; }
        revision=activePointer.revision;
        desiredLog=Math.Max(Math.Log(minimumFactor),Math.Min(Math.Log(maximumFactor),anchorLogScale+rangeExponent*(Math.Log(range)-anchorLogRange)));
        desiredFactor=(float)Math.Exp(desiredLog);
        double next=desiredLog;
        if(response>0)
        {
            // Same analytic log-target follower as ordinary Unity; no queued tween.
            double interval=response*dt,decay=Math.Exp(-interval);
            double weight=interval<.001?interval*(1-interval*.5+interval*interval/6):1-decay;
            double ramp=interval<.001?interval*(.5-interval/6+interval*interval/24):1-weight/interval;
            next=currentLog+(previousDesired-currentLog)*weight+(desiredLog-previousDesired)*ramp;
        }
        previousDesired=desiredLog; Apply(next);
    }
    private Matrix4x4 ParentFrame() { return scaleTarget.parent==null?Matrix4x4.identity:scaleTarget.parent.localToWorldMatrix; }
    private void EndGesture()
    {
        BirdUiPointer previous=activePointer; activePointer=null; owner=null; desiredLog=previousDesired=currentLog; desiredFactor=factor;
        if(previous!=null) Notify(stoppedEvent);
    }
    private void Apply(double logarithm)
    {
        if(bound==null) return;
        logarithm=Math.Max(Math.Log(minimumFactor),Math.Min(Math.Log(maximumFactor),logarithm));
        float nextFactor=(float)Math.Exp(logarithm); Vector3 size=restScale*nextFactor;
        if(!ValidScale(size)) { Cancel(); return; }
        double prior=currentLog; currentLog=logarithm; factor=nextFactor; expectedScale=size; bound.localScale=size; Physics.SyncTransforms();
        if(Math.Abs(prior-currentLog)>1e-9) Notify(changedEvent);
    }
    private void Notify(string name) { if(eventTarget!=null && !string.IsNullOrEmpty(name)) eventTarget.SendCustomEvent(name); }
    private bool ValidScale(Vector3 v) { return Finite(v.sqrMagnitude) && Mathf.Abs(v.x)>1e-7f && Mathf.Abs(v.y)>1e-7f && Mathf.Abs(v.z)>1e-7f; }
    private bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }
}
