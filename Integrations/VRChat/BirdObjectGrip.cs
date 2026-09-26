using UdonSharp;
using UnityEngine;

/// <summary>One local transaction at a time, selected by either hand. Translation only; kinematic volumes.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(120)]
public class BirdObjectGrip : UdonSharpBehaviour
{
    public BirdUiPointer[] pointers=new BirdUiPointer[0];
    public BirdObjectTarget[] targets=new BirdObjectTarget[0];
    [Min(.01f)] public float followRate=18;
    [Min(.01f)] public float returnDuration=.25f;
    public bool automatic=true;
    [HideInInspector] public BirdObjectTarget ActiveTarget;
    [HideInInspector] public BirdUiPointer ActivePointer;
    [HideInInspector] public BirdObjectSnapTarget Candidate;
    [HideInInspector] public BirdObjectTarget HoveredTarget;
    [HideInInspector] public bool ReadyToPlace;
    [HideInInspector] public bool IsReturning;
    [HideInInspector] public Vector3 RawDesired;
    private int[] revisions=new int[0];
    public BirdUiPanel[] menuPanels=new BirdUiPanel[0];
    public BirdObjectPlayArea playArea;
    [HideInInspector] public float stepDelta=1f/72;
    BirdObjectRegion region;
    BirdObjectPolicy rule;
    string owner;
    Vector3 home,anchor,previousDesired,returnStart,shapeCenter,shapeSize;
    Quaternion rotation;
    Vector3 scale;
    Transform parent;
    Matrix4x4 frame;
    Bounds allowed;
    float returnElapsed;
    bool processing;

    public void Initialize() { if(processing) return; CancelImmediately(); Seed(); }
    void Start() { Seed(); }
    void OnEnable() { Seed(); }
    void Seed()
    {
        if(revisions.Length!=pointers.Length) revisions=new int[pointers.Length];
        for(int i=0;i<pointers.Length;i++) if(pointers[i]!=null) revisions[i]=pointers[i].revision;
    }
    void OnDisable() { CancelImmediately(); Seed(); }
    void LateUpdate() { if(automatic) { stepDelta=Time.deltaTime; Process(); } }

    public void Process()
    {
        if(processing || !enabled || !gameObject.activeInHierarchy) return;
        float dt=stepDelta;
        if(revisions.Length!=pointers.Length) Seed();
        processing=true;

        if(!Finite(dt) || dt<=0) { CancelImmediately(); Seed(); processing=false; return; }
        if(ActiveTarget!=null)
        {
            HoveredTarget=null;
            if(IsReturning) { AdvanceReturn(Mathf.Min(dt,.25f)); Seed(); processing=false; return; }
            var p=ActivePointer;
            if(dt>.25f || p==null || !p.IsTracked() || !p.hasHistory || p.userId!=owner || p.uiConsumed || Blocked() || !ValidHeld())
            { Cancel(); Seed(); processing=false; return; }
            Vector3 raw=home+(region.transform.InverseTransformPoint(p.position)-anchor);
            if(!FiniteVector(raw)) { Cancel(); Seed(); processing=false; return; }
            RawDesired=raw;
            Vector3 desired=Guide(raw);
            float rate=Finite(followRate)?Mathf.Max(.01f,followRate):18;
            Vector3 current=region.transform.InverseTransformPoint(ActiveTarget.transform.position);
            // Exact first-order response to a linearly moving target during this step.
            float interval=rate*dt;
            float decay=Mathf.Exp(-interval);
            float response=interval<.001f?interval*(1-interval*.5f+interval*interval/6):1-decay;
            float ramp=interval<.001f?interval*(.5f-interval/6+interval*interval/24):1-response/interval;
            Vector3 next=current+(previousDesired-current)*response+(desired-previousDesired)*ramp;
            previousDesired=desired;
            Move(region.Clamp(allowed,next));
            if(ReadyToPlace) ReadyToPlace=Vector3.Distance(region.transform.InverseTransformPoint(ActiveTarget.transform.position),region.transform.InverseTransformPoint(Candidate.transform.position))<=Candidate.captureRadius*1.5f;
            if(!p.pressed) FinishDrop();
            Seed(); processing=false; return;
        }
        // Consume all revisions before callbacks can re-enter or reconfigure input.
        BirdObjectTarget best=null; BirdUiPointer hand=null;
        float nearest=float.PositiveInfinity,hoverDistance=float.PositiveInfinity; HoveredTarget=null;
        for(int index=0;index<pointers.Length;index++)
        {
            BirdUiPointer p=pointers[index];
            if(p==null) continue;
            bool fresh=revisions[index]!=p.revision;
            revisions[index]=p.revision;
            if(!p.IsTracked() || p.uiConsumed || Blocked()) continue;
            foreach(var item in targets)
            {
                if(item==null || item.owner!=null || !item.Available()) continue;
                float distance;
                if(!Hit(item.volume,p,out distance)) continue;
                if(distance<hoverDistance) { HoveredTarget=item; hoverDistance=distance; }
                if(fresh && p.pressedThisSample && distance<nearest) { best=item; hand=p; nearest=distance; }
            }
        }
        if(best!=null) Begin(best,hand);
        processing=false;
    }

    private bool Hit(BoxCollider volume,BirdUiPointer pointer,out float distance)
    {
        distance=0;
        if((volume.ClosestPoint(pointer.origin)-pointer.origin).sqrMagnitude<1e-10f) return true;
        Vector3 delta=pointer.position-pointer.origin; float length=delta.magnitude;
        RaycastHit hit;
        if(length<1e-6f) return (volume.ClosestPoint(pointer.position)-pointer.position).sqrMagnitude<1e-10f;
        if(!volume.Raycast(new Ray(pointer.origin,delta/length),out hit,length)) return false;
        distance=hit.distance; return true;
    }
    void Begin(BirdObjectTarget item,BirdUiPointer pointer)
    {
        if(!item.region.TryPivotBounds(item.transform,item.volume)) return;
        Bounds bounds=item.region.pivotBounds;
        Vector3 start=item.region.transform.InverseTransformPoint(item.transform.position);
        if((item.region.Clamp(bounds,start)-start).sqrMagnitude>1e-8f) return;
        if(item.policy!=null && !item.policy.Query(BirdObjectOperation.Begin,item,null)) return;
        ActiveTarget=item; ActivePointer=pointer; region=item.region; rule=item.policy; owner=pointer.userId;
        home=previousDesired=start; anchor=region.transform.InverseTransformPoint(pointer.position);
        parent=item.transform.parent; rotation=item.transform.localRotation; scale=item.transform.localScale;
        frame=region.transform.localToWorldMatrix; allowed=bounds;
        shapeCenter=item.volume.center; shapeSize=item.volume.size;
        RawDesired=home; Candidate=null; ReadyToPlace=false; IsReturning=false; item.owner=this;
        item.Notify(0);
    }
    bool ValidHeld()
    {
        var t=ActiveTarget;

        return t!=null && t.enabled && t.gameObject.activeInHierarchy && t.volume!=null && t.volume.enabled && t.volume.gameObject.activeInHierarchy &&
            region!=null && region.enabled && region.gameObject.activeInHierarchy && t.region==region && t.policy==rule &&
            (rule==null || rule.enabled && rule.gameObject.activeInHierarchy) && region.transform.localToWorldMatrix==frame &&
            t.transform.parent==parent && t.transform.localRotation==rotation && t.transform.localScale==scale &&
            t.volume.center==shapeCenter && t.volume.size==shapeSize &&
            (t.volume.attachedRigidbody==null || t.volume.attachedRigidbody.isKinematic) &&
            region.TryPivotBounds(t.transform,t.volume) &&
            (region.pivotBounds.min-allowed.min).sqrMagnitude<1e-7f && (region.pivotBounds.max-allowed.max).sqrMagnitude<1e-7f;
    }
    Vector3 Guide(Vector3 raw)
    {

        if(Candidate==null || !Eligible(Candidate) || !Candidate.Evaluate(region,raw) ||
            Candidate.distance>Candidate.influenceRadius*Candidate.exitMultiplier) Candidate=null;
        if(Candidate==null)
        {
            float best=float.PositiveInfinity;
            foreach(var slot in ActiveTarget.destinations)
            {
                if(slot==null || !Eligible(slot) || !slot.Evaluate(region,raw)) continue;
                float normalized=slot.distance/slot.influenceRadius;
                if(normalized<=1 && normalized<best) { best=normalized; Candidate=slot; }
            }
        }
        ReadyToPlace=false;
        Vector3 desired=raw;
        if(Candidate!=null && Candidate.Evaluate(region,raw))
        {
            desired=Candidate.guided;
            // Never qualify a drop using the clamped or magnetically guided pose.
            ReadyToPlace=Vector3.Distance(raw,Candidate.end)<=Candidate.captureRadius &&
                (region.Clamp(allowed,Candidate.end)-Candidate.end).sqrMagnitude<1e-8f;
        }
        return region.Clamp(allowed,desired);
    }
    bool Eligible(BirdObjectSnapTarget slot)
    {
        return slot.enabled && slot.gameObject.activeInHierarchy && Registered(slot) &&
            (rule==null || (rule.enabled && rule.gameObject.activeInHierarchy && rule.Query(BirdObjectOperation.CanPlace,ActiveTarget,slot)));
    }
    void FinishDrop()
    {
        var item=ActiveTarget; var slot=Candidate;
        if(!ReadyToPlace || slot==null || !Eligible(slot) || (rule!=null && !rule.Query(BirdObjectOperation.Commit,item,slot))) { Cancel(); return; }
        Move(region.transform.InverseTransformPoint(slot.transform.position));
        Clear(); item.Notify(1);
    }
    void Move(Vector3 point)
    {
        if(ActiveTarget==null || region==null) return;
        ActiveTarget.transform.position=region.transform.TransformPoint(point); Physics.SyncTransforms();
    }
    [RecursiveMethod]
    public void Cancel()
    {
        Seed();
        if(ActiveTarget==null || IsReturning) return;
        var item=ActiveTarget;
        if(rule!=null) rule.Query(BirdObjectOperation.Cancel,item,null);
        Candidate=null; ReadyToPlace=false; ActivePointer=null; IsReturning=true; returnElapsed=0;
        returnStart=region!=null?region.transform.InverseTransformPoint(item.transform.position):home;
        item.Notify(2);
    }
    void AdvanceReturn(float dt)
    {
        if(region==null) { Clear(); return; }
        returnElapsed+=dt;
        float duration=Finite(returnDuration)?Mathf.Max(.01f,returnDuration):.25f;
        float t=Mathf.Clamp01(returnElapsed/duration);
        Move(Vector3.Lerp(returnStart,home,t*t*(3-2*t)));
        if(t>=1) Clear();
    }
    [RecursiveMethod]
    public void CancelImmediately()
    {
        Seed();
        if(ActiveTarget==null) { Clear(); return; }
        var item=ActiveTarget; bool notify=!IsReturning;
        if(rule!=null) rule.Query(BirdObjectOperation.Cancel,item,null);
        Move(home); Clear(); if(notify) item.Notify(2);
    }
    void Clear()
    {
        if(ActiveTarget!=null && ActiveTarget.owner==this) ActiveTarget.owner=null;
        ActiveTarget=null; ActivePointer=null; Candidate=null; HoveredTarget=null; ReadyToPlace=false; IsReturning=false; region=null; rule=null;
    }
    private bool Registered(BirdObjectSnapTarget slot)
    {
        foreach(BirdObjectSnapTarget value in ActiveTarget.destinations) if(value==slot) return true;
        return false;
    }
    private bool Blocked()
    {
        if(playArea!=null && (!playArea.enabled || !playArea.gameObject.activeInHierarchy || !playArea.allowed)) return true;
        foreach(BirdUiPanel panel in menuPanels) if(panel!=null && panel.enabled && panel.gameObject.activeInHierarchy && panel.state!=0) return true;
        return false;
    }
    private bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    private bool FiniteVector(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }

}
