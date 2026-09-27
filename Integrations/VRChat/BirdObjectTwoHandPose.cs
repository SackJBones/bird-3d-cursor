using UdonSharp;
using UnityEngine;
using VRC.Udon;

/// <summary>Optional local second-hand clutch. Bird points acquire; hand origins turn and resize.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(110)]
public class BirdObjectTwoHandPose : UdonSharpBehaviour
{
    public BirdObjectGrip grip;
    public BirdUiPointer first, second;
    [Tooltip("Input origins must be tracked hand roots in world metres, not a shared head/ray origin.")]
    [Min(.001f)] public float minimumSpan=.06f;
    [Min(.001f)] public float maximumSpan=3;
    [Min(.001f)] public float maximumSampleGap=.25f;
    [Tooltip("Release and re-clutch before the ambiguous opposite-vector rotation.")]
    [Range(30,175)] public float maximumTurnDegrees=160;
    public bool automatic=true;
    [Tooltip("Match the grip and router phase when consuming after-IK avatar input.")]
    public bool postLateUpdate;
    public UdonBehaviour eventTarget;
    public string engagedEvent, disengagedEvent;
    [HideInInspector] public BirdUiPointer ActiveSecondary;
    [HideInInspector] public bool IsEngaged, GestureLimited;
    [HideInInspector] public float stepDelta=1f/72;

    private BirdUiPointer primary, boundFirst, boundSecond;
    private BirdObjectGrip boundGrip;
    private BirdObjectTarget target;
    private Transform parent;
    private Matrix4x4 parentFrame, regionFrame;
    private Quaternion startWorldRotation, lastRequest;
    private Vector3 startDirection;
    private float startSpan, startFactor, lastFactor, firstAge, secondAge;
    private int firstRevision, secondRevision, appliedFirst, appliedSecond;
    private int automaticFrame=-1;
    private string owner;
    private bool processing, boundPhase;

    private void Start() { Seed(); }
    private void OnEnable() { Seed(); }
    private void OnDisable() { End(true,false); Seed(); }
    private void LateUpdate() { if(!postLateUpdate) AutomaticStep(); }
    public override void PostLateUpdate() { if(postLateUpdate) AutomaticStep(); }
    private void AutomaticStep()
    {
        if(!automatic || !enabled || !gameObject.activeInHierarchy || automaticFrame==Time.frameCount) return;
        automaticFrame=Time.frameCount; stepDelta=Time.deltaTime; Process();
    }
    private void Seed()
    {
        boundFirst=first; boundSecond=second; boundGrip=grip; boundPhase=postLateUpdate;
        firstRevision=first!=null?first.revision:0; secondRevision=second!=null?second.revision:0;
        firstAge=secondAge=0;
    }
    [RecursiveMethod]
    public void Cancel() { End(true,false); Seed(); }
    [RecursiveMethod]
    public void Process()
    {
        if(processing || !enabled || !gameObject.activeInHierarchy) return;
        processing=true; Step(); processing=false;
    }
    private void Step()
    {
        if(first!=boundFirst || second!=boundSecond || grip!=boundGrip || postLateUpdate!=boundPhase)
        { End(true,false); Seed(); return; }
        float dt=stepDelta;
        if(!ValidSettings() || !Finite(dt) || dt<=0 || dt>maximumSampleGap)
        { End(true,true); Seed(); return; }
        bool newFirst=first!=null && first.revision!=firstRevision;
        bool newSecond=second!=null && second.revision!=secondRevision;
        firstAge=newFirst?0:firstAge+dt; secondAge=newSecond?0:secondAge+dt;
        firstRevision=first!=null?first.revision:0; secondRevision=second!=null?second.revision:0;
        if(grip==null || !grip.enabled || !grip.gameObject.activeInHierarchy || first==null || second==null || first==second ||
            (automatic && grip.automatic && postLateUpdate!=grip.postLateUpdate))
        { End(true,true); return; }
        if(IsEngaged)
        {
            if(!Held() || !Tracked(primary) || !Tracked(ActiveSecondary) ||
                !grip.AllowsInput(primary) || !grip.AllowsInput(ActiveSecondary) || firstAge>maximumSampleGap || secondAge>maximumSampleGap)
            { End(true,true); return; }
            // An explicit pose command takes ownership without this adapter overwriting it.
            if(grip.RequestedLocalRotation!=lastRequest || grip.RequestedScaleFactor!=lastFactor)
            { End(false,false); return; }
            if(!primary.pressed)
            {
                if(newFirst || newSecond) Apply();
                End(false,false); return;
            }
            if(!ActiveSecondary.pressed) { End(true,false); return; }
            if(firstRevision!=appliedFirst && secondRevision!=appliedSecond) Apply();
            return;
        }
        GestureLimited=false;
        var item=grip.ActiveTarget; var hand=grip.ActivePointer;
        if(item==null || grip.IsReturning || !item.enabled || !item.gameObject.activeInHierarchy || item.region==null ||
            !item.region.enabled || !item.region.gameObject.activeInHierarchy || (!item.allowRotation && !item.allowScaling) ||
            !Tracked(hand) || !hand.pressed || !grip.AllowsInput(hand) || firstAge>maximumSampleGap || secondAge>maximumSampleGap) return;
        var other=hand==first?second:hand==second?first:null;
        bool fresh=other==first?newFirst:newSecond;
        if(other==null || !fresh || !Tracked(other) || !grip.AllowsInput(other) || !other.pressedThisSample ||
            other.userId!=hand.userId || !Hit(item.volume,other)) return;
        Vector3 direction; float length;
        if(!Span(hand,other,out direction,out length)) return;
        target=item; primary=hand; ActiveSecondary=other; IsEngaged=true; owner=hand.userId; parent=item.transform.parent;
        parentFrame=parent!=null?parent.localToWorldMatrix:Matrix4x4.identity; regionFrame=item.region.transform.localToWorldMatrix;
        startDirection=direction; startSpan=length; startFactor=grip.CurrentScaleFactor;
        startWorldRotation=item.region.AuthoredRotation(parent)*item.transform.localRotation;
        if(!grip.TrySetHeldPose(item.transform.localRotation,startFactor)) { End(false,true); return; }
        RememberRequest(); Notify(true);
    }
    private bool Held()
    {
        return target!=null && primary!=null && ActiveSecondary!=null && target.enabled && target.gameObject.activeInHierarchy &&
            grip.ActiveTarget==target && grip.ActivePointer==primary && target.owner==grip && !grip.IsReturning &&
            target.region!=null && target.region.enabled && target.region.gameObject.activeInHierarchy && target.transform.parent==parent &&
            (parent!=null?parent.localToWorldMatrix:Matrix4x4.identity)==parentFrame && target.region.transform.localToWorldMatrix==regionFrame &&
            primary.userId==owner && ActiveSecondary.userId==owner;
    }
    private void Apply()
    {
        Vector3 direction; float length;
        if(!Span(primary,ActiveSecondary,out direction,out length) || Vector3.Angle(startDirection,direction)>maximumTurnDegrees)
        { End(true,true); return; }
        Quaternion orientation=target.allowRotation?Quaternion.Inverse(target.region.AuthoredRotation(parent))*
            Quaternion.FromToRotation(startDirection,direction)*startWorldRotation:target.transform.localRotation;
        float factor=target.allowScaling?(float)((double)startFactor*length/startSpan):startFactor;
        GestureLimited=!grip.TrySetHeldPose(orientation,factor); RememberRequest();
    }
    private void RememberRequest()
    {
        lastRequest=grip.RequestedLocalRotation; lastFactor=grip.RequestedScaleFactor;
        appliedFirst=firstRevision; appliedSecond=secondRevision;
    }
    [RecursiveMethod]
    private void End(bool freeze,bool limited)
    {
        bool notify=IsEngaged;
        if(freeze && notify && boundGrip!=null && boundGrip.ActiveTarget==target && !boundGrip.IsReturning) boundGrip.StopHeldPose(limited);
        IsEngaged=false; ActiveSecondary=null; target=null; primary=null; GestureLimited=limited;
        if(notify) Notify(false);
    }
    [RecursiveMethod]
    private void Notify(bool engaging)
    {
        string eventName=engaging?engagedEvent:disengagedEvent;
        if(eventTarget!=null && !string.IsNullOrEmpty(eventName)) eventTarget.SendCustomEvent(eventName);
    }
    private bool Span(BirdUiPointer a,BirdUiPointer b,out Vector3 direction,out float length)
    {
        Vector3 delta=b.origin-a.origin; length=delta.magnitude; direction=Vector3.zero;
        if(!FiniteVector(delta) || !Finite(length) || length<minimumSpan || length>maximumSpan) return false;
        direction=delta/length; return true;
    }
    private bool ValidSettings()
    {
        return Finite(minimumSpan) && Finite(maximumSpan) && minimumSpan>0 && maximumSpan>=minimumSpan &&
            Finite(maximumSampleGap) && maximumSampleGap>0 && maximumSampleGap<=.25f &&
            Finite(maximumTurnDegrees) && maximumTurnDegrees>=30 && maximumTurnDegrees<=175;
    }
    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    private bool Tracked(BirdUiPointer value) { return value!=null && value.IsTracked() && value.hasHistory; }
    private bool Hit(BoxCollider volume,BirdUiPointer pointer)
    {
        if(volume==null || !volume.enabled || !volume.gameObject.activeInHierarchy) return false;
        if((volume.ClosestPoint(pointer.origin)-pointer.origin).sqrMagnitude<1e-10f) return true;
        Vector3 delta=pointer.position-pointer.origin; float length=delta.magnitude; RaycastHit hit;
        return length>1e-6f && volume.Raycast(new Ray(pointer.origin,delta/length),out hit,length);
    }
}
