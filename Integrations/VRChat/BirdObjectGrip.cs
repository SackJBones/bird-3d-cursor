using System;
using UdonSharp;
using UnityEngine;

/// <summary>One local transaction at a time, selected by either hand. Optional bounded pose; kinematic volumes.</summary>
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
    [HideInInspector] public Quaternion RequestedLocalRotation=Quaternion.identity;
    [HideInInspector] public float RequestedScaleFactor=1,CurrentScaleFactor=1;
    [HideInInspector] public bool PoseLimited;
    // Set these arguments and send RequestPose from another Udon program or test harness.
    [HideInInspector] public Quaternion poseRequestRotation=Quaternion.identity;
    [HideInInspector] public float poseRequestFactor=1;
    [HideInInspector] public bool poseRequestAccepted;
    public void RequestPose() { poseRequestAccepted=TrySetHeldPose(poseRequestRotation,poseRequestFactor); }
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
    Quaternion homeRotation,returnRotation,poseStartRotation;
    Vector3 homeScale,returnScale,referenceScale;
    float homeFactor,minimumFactor,maximumFactor;
    bool poseRotation,poseScaling,hasReferenceScale;
    Transform parent;
    Matrix4x4 frame,parentFrame;
    double poseResponseIntegral,poseStartLogScale;
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
            AdvancePose(dt);
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
        float factor=1;
        bool knownScale=item.TryScaleFactor(item.transform.localScale);
        factor=item.evaluatedScaleFactor;
        if(item.allowScaling && (!knownScale || !item.AllowsFactor(factor))) return;
        if(!knownScale) factor=1;
        Vector3 start=item.region.transform.InverseTransformPoint(item.transform.position);
        if((item.region.Clamp(bounds,start)-start).sqrMagnitude>1e-8f) return;
        if(item.policy!=null && !item.policy.Query(BirdObjectOperation.Begin,item,null)) return;
        ActiveTarget=item; ActivePointer=pointer; region=item.region; rule=item.policy; owner=pointer.userId;
        home=previousDesired=start; anchor=region.transform.InverseTransformPoint(pointer.position);
        parent=item.transform.parent; rotation=item.transform.localRotation; scale=item.transform.localScale;
        homeRotation=RequestedLocalRotation=rotation; homeScale=scale;
        homeFactor=CurrentScaleFactor=RequestedScaleFactor=factor; PoseLimited=false;
        poseStartRotation=rotation; poseStartLogScale=Math.Log(factor); poseResponseIntegral=0;
        poseRotation=item.allowRotation; poseScaling=item.allowScaling; referenceScale=item.poseReferenceScale;
        hasReferenceScale=knownScale;
        minimumFactor=item.minimumScaleFactor; maximumFactor=item.maximumScaleFactor;
        frame=region.transform.localToWorldMatrix; parentFrame=parent!=null?parent.localToWorldMatrix:Matrix4x4.identity; allowed=bounds;
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
            (parent!=null?parent.localToWorldMatrix:Matrix4x4.identity)==parentFrame &&
            t.allowRotation==poseRotation && t.allowScaling==poseScaling && t.poseReferenceScale==referenceScale &&
            t.minimumScaleFactor==minimumFactor && t.maximumScaleFactor==maximumFactor &&
            t.volume.center==shapeCenter && t.volume.size==shapeSize &&
            (t.volume.attachedRigidbody==null || t.volume.attachedRigidbody.isKinematic) &&
            region.TryPivotBounds(t.transform,t.volume) &&
            (region.pivotBounds.min-allowed.min).sqrMagnitude<1e-7f && (region.pivotBounds.max-allowed.max).sqrMagnitude<1e-7f;
    }
    /// <summary>Request an absolute local orientation and authored-reference scale factor. Does not mutate the target until Process.</summary>
    public bool TrySetHeldPose(Quaternion localRotation,float factor)
    {
        Quaternion unit; Bounds bounds;
        if((!enabled || !gameObject.activeInHierarchy) || ActiveTarget==null || IsReturning) return false;
        if(!Normalize(localRotation,out unit) || !PoseAllowed(unit,factor,out bounds))
        { StopPoseRequest(); return false; }
        if(unit.x!=RequestedLocalRotation.x || unit.y!=RequestedLocalRotation.y || unit.z!=RequestedLocalRotation.z || unit.w!=RequestedLocalRotation.w || factor!=RequestedScaleFactor)
        { poseStartRotation=rotation; poseStartLogScale=Math.Log(CurrentScaleFactor); poseResponseIntegral=0; }
        RequestedLocalRotation=unit; RequestedScaleFactor=factor; PoseLimited=false; return true;
    }
    public void ResetHeldPose() { TrySetHeldPose(homeRotation,homeFactor); }
    bool PoseAllowed(Quaternion orientation,float factor,out Bounds bounds)
    {
        bounds=new Bounds();
        if(ActiveTarget==null || region==null || !Finite(factor) || factor<=0 ||
            (!poseRotation && Quaternion.Angle(orientation,homeRotation)>.001f) ||
            (poseScaling?!ActiveTarget.AllowsFactor(factor):Mathf.Abs(factor-homeFactor)>1e-6f)) return false;
        if(!region.TryPoseBounds(ActiveTarget.transform,ActiveTarget.volume,orientation,poseScaling?referenceScale*factor:homeScale)) return false;
        bounds=region.pivotBounds; return true;
    }
    void AdvancePose(float dt)
    {
        if(!poseRotation && !poseScaling) return;
        float rate=Finite(followRate)?Mathf.Max(.01f,followRate):18;
        poseResponseIntegral+=rate*(double)dt;
        double response=1-Math.Exp(-poseResponseIntegral);
        // Evaluate from this request's anchor, avoiding repeated small-angle slerp drift.
        Quaternion nextRotation=Quaternion.Slerp(poseStartRotation,RequestedLocalRotation,(float)response);
        float nextFactor=(float)Math.Exp(poseStartLogScale+(Math.Log(RequestedScaleFactor)-poseStartLogScale)*response);
        Bounds bounds;
        if(!PoseAllowed(nextRotation,nextFactor,out bounds))
        {
            // A feasible end orientation can have an infeasible intermediate swept box.
            // Stop this request at the last valid sampled pose rather than forcing an invalid box.
            StopPoseRequest(); return;
        }
        rotation=nextRotation; CurrentScaleFactor=nextFactor; scale=poseScaling?referenceScale*nextFactor:homeScale;
        ActiveTarget.transform.localRotation=rotation; ActiveTarget.transform.localScale=scale; allowed=bounds;
    }
    void StopPoseRequest()
    {
        RequestedLocalRotation=poseStartRotation=rotation; RequestedScaleFactor=CurrentScaleFactor;
        poseStartLogScale=Math.Log(CurrentScaleFactor); poseResponseIntegral=0;
        PoseLimited=true; ReadyToPlace=false;
    }
    bool DestinationPose(BirdObjectSnapTarget slot,out Quaternion orientation,out float factor,out Bounds bounds)
    {
        orientation=rotation; factor=CurrentScaleFactor; bounds=allowed;
        if(PoseLimited) return false;
        if(slot.matchRotation)
        {
            if(!Finite(slot.rotationCaptureDegrees) || slot.rotationCaptureDegrees<0 || slot.rotationCaptureDegrees>180) return false;
            Quaternion desired=Quaternion.Inverse(region.AuthoredRotation(parent))*region.AuthoredRotation(slot.transform);
            if(!Normalize(desired,out orientation) ||
                Quaternion.Angle(RequestedLocalRotation,orientation)>slot.rotationCaptureDegrees ||
                Quaternion.Angle(rotation,orientation)>slot.rotationCaptureDegrees) return false;
        }
        if(slot.matchScale)
        {
            if(!hasReferenceScale) return false;
            factor=slot.scaleFactor;
            if(!Finite(factor) || factor<=0 || !Finite(slot.scaleCaptureRatio) || slot.scaleCaptureRatio<0) return false;
            float tolerance=Mathf.Log(1+slot.scaleCaptureRatio);
            if(Mathf.Abs(Mathf.Log(RequestedScaleFactor/factor))>tolerance || Mathf.Abs(Mathf.Log(CurrentScaleFactor/factor))>tolerance) return false;
        }
        return PoseAllowed(orientation,factor,out bounds);
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
            Quaternion finalRotation; float finalFactor; Bounds finalBounds;
            ReadyToPlace=Vector3.Distance(raw,Candidate.end)<=Candidate.captureRadius &&
                DestinationPose(Candidate,out finalRotation,out finalFactor,out finalBounds) &&
                (region.Clamp(finalBounds,Candidate.end)-Candidate.end).sqrMagnitude<1e-8f;
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
        Quaternion finalRotation; float finalFactor; Bounds finalBounds;
        if(!ReadyToPlace || slot==null || !Eligible(slot) || !DestinationPose(slot,out finalRotation,out finalFactor,out finalBounds) || (rule!=null && !rule.Query(BirdObjectOperation.Commit,item,slot))) { Cancel(); return; }
        if(poseRotation) item.transform.localRotation=finalRotation;
        if(poseScaling) item.transform.localScale=referenceScale*finalFactor;
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
        returnRotation=item.transform.localRotation; returnScale=item.transform.localScale;
        item.Notify(2);
    }
    void AdvanceReturn(float dt)
    {
        if(region==null) { Clear(); return; }
        returnElapsed+=dt;
        float duration=Finite(returnDuration)?Mathf.Max(.01f,returnDuration):.25f;
        float t=Mathf.Clamp01(returnElapsed/duration);
        float weight=t*t*(3-2*t);
        Vector3 point=Vector3.Lerp(returnStart,home,weight);
        if(poseRotation || poseScaling)
        {
            Quaternion turn=poseRotation?Quaternion.Slerp(returnRotation,homeRotation,weight):ActiveTarget.transform.localRotation;
            Vector3 size=poseScaling?Vector3.Lerp(returnScale,homeScale,weight):ActiveTarget.transform.localScale;
            if(!region.TryPoseBounds(ActiveTarget.transform,ActiveTarget.volume,turn,size))
            { RestoreHomePose(); Move(home); Clear(); return; }
            ActiveTarget.transform.localRotation=turn; ActiveTarget.transform.localScale=size;
            point=region.Clamp(region.pivotBounds,point);
        }
        Move(point);
        if(t>=1) Clear();
    }
    [RecursiveMethod]
    public void CancelImmediately()
    {
        Seed();
        if(ActiveTarget==null) { Clear(); return; }
        var item=ActiveTarget; bool notify=!IsReturning;
        if(rule!=null) rule.Query(BirdObjectOperation.Cancel,item,null);
        RestoreHomePose(); Move(home); Clear(); if(notify) item.Notify(2);
    }
    void RestoreHomePose()
    {
        if(ActiveTarget==null) return;
        if(poseRotation) ActiveTarget.transform.localRotation=homeRotation;
        if(poseScaling) ActiveTarget.transform.localScale=homeScale;
    }
    void Clear()
    {
        if(ActiveTarget!=null && ActiveTarget.owner==this) ActiveTarget.owner=null;
        ActiveTarget=null; ActivePointer=null; Candidate=null; HoveredTarget=null; ReadyToPlace=false; IsReturning=false; region=null; rule=null;
        RequestedLocalRotation=Quaternion.identity; RequestedScaleFactor=CurrentScaleFactor=1; PoseLimited=false; poseRequestAccepted=false;
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

    private bool Normalize(Quaternion value,out Quaternion unit)
    {
        unit=Quaternion.identity;
        if(!Finite(value.x)||!Finite(value.y)||!Finite(value.z)||!Finite(value.w)) return false;
        double length=System.Math.Sqrt((double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z+(double)value.w*value.w);
        if(length<1e-12) return false;
        unit=new Quaternion((float)(value.x/length),(float)(value.y/length),(float)(value.z/length),(float)(value.w/length)); return true;
    }
}
