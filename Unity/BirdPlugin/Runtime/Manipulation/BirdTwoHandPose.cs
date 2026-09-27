using UnityEngine;
using UnityEngine.Events;
using Bird3DCursor.UI;

namespace Bird3DCursor.Manipulation
{
    /// <summary>Optional second-hand clutch. Bird points acquire; hand origins supply relative turn and size.</summary>
    [AddComponentMenu("Bird/Manipulation/Two Hand Pose")]
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class BirdTwoHandPose : MonoBehaviour
    {
        public BirdGrabInteractor interactor;
        public BirdPointerInput first,second;
        [Tooltip("Input origins must be tracked hand roots in world metres, not a shared head/ray origin.")]
        [Min(.001f)] public float minimumSpan=.06f;
        [Min(.001f)] public float maximumSpan=3;
        [Min(.001f)] public float maximumSampleGap=.25f;
        [Tooltip("A clutch stops before the ambiguous opposite-vector rotation. Release and re-grip to turn further.")]
        [Range(30,175)] public float maximumTurnDegrees=160;
        public bool automatic=true;
        [SerializeField] UnityEvent engaged=new UnityEvent(),disengaged=new UnityEvent();
        public UnityEvent Engaged { get { return engaged; } }
        public UnityEvent Disengaged { get { return disengaged; } }
        public BirdPointerInput ActiveSecondary { get; private set; }
        public bool IsEngaged { get { return clutchActive; } }
        public bool GestureLimited { get; private set; }
        BirdPointerInput primary,boundFirst,boundSecond;
        BirdGrabInteractor boundInteractor;
        BirdGrabTarget target;
        Transform parent;
        Matrix4x4 parentFrame,regionFrame;
        Quaternion startWorldRotation,lastRequest;
        Vector3 startDirection;
        float startSpan,startFactor,lastFactor,firstAge,secondAge;
        uint firstRevision,secondRevision,appliedFirst,appliedSecond;
        string owner;
        bool processing,clutchActive;

        void OnEnable() { Seed(); }
        void OnDisable() { End(true,false); Seed(); }
        void LateUpdate() { if(automatic) Process(Time.deltaTime); }
        void Seed()
        {
            boundFirst=first; boundSecond=second; boundInteractor=interactor;
            firstRevision=first!=null?first.Revision:0; secondRevision=second!=null?second.Revision:0;
            firstAge=secondAge=0;
        }
        public void Cancel() { End(true,false); Seed(); }
        public void Process(float dt)
        {
            if(processing || !isActiveAndEnabled) return;
            processing=true;
            try
            {
                if(boundFirst!=first || boundSecond!=second || boundInteractor!=interactor)
                { End(true,false); Seed(); return; }
                if(!ValidSettings() || !BirdPlacementRegion.Finite(dt) || dt<=0 || dt>maximumSampleGap)
                { End(true,true); Seed(); return; }
                bool newFirst=first!=null && first.Revision!=firstRevision;
                bool newSecond=second!=null && second.Revision!=secondRevision;
                firstAge=newFirst?0:firstAge+dt; secondAge=newSecond?0:secondAge+dt;
                firstRevision=first!=null?first.Revision:0; secondRevision=second!=null?second.Revision:0;
                if(interactor==null || !interactor.isActiveAndEnabled || first==null || second==null || first==second)
                { End(true,true); return; }
                if(IsEngaged)
                {
                    if(!Held() || !Tracked(primary) || !Tracked(ActiveSecondary) || firstAge>maximumSampleGap || secondAge>maximumSampleGap)
                    { End(true,true); return; }
                    // Another explicit pose controller takes over without this adapter overwriting its command.
                    if(interactor.RequestedLocalRotation!=lastRequest || interactor.RequestedScaleFactor!=lastFactor)
                    { End(false,false); return; }
                    if(!primary.IsPressed)
                    {
                        // Include the final available gesture sample before primary release is judged by the grip.
                        if(newFirst || newSecond) Apply();
                        End(false,false); return;
                    }
                    if(!ActiveSecondary.IsPressed) { End(true,false); return; }
                    // Pair fresh samples; a fast hand cannot repeatedly consume stale coordinates from the other.
                    if(firstRevision!=appliedFirst && secondRevision!=appliedSecond) Apply();
                    return;
                }
                GestureLimited=false;
                var item=interactor.ActiveTarget; var hand=interactor.ActivePointer;
                if(item==null || interactor.IsReturning || !item.isActiveAndEnabled || item.Region==null || !item.Region.isActiveAndEnabled || (!item.AllowRotation && !item.AllowScaling) ||
                    hand==null || !Tracked(hand) || !hand.IsPressed || firstAge>maximumSampleGap || secondAge>maximumSampleGap) return;
                var other=hand==first?second:hand==second?first:null;
                bool fresh=other==first?newFirst:newSecond;
                if(other==null || !fresh || !Tracked(other) || !other.PressedThisSample || other.UserId!=hand.UserId || !Hit(item.Volume,other)) return;
                Vector3 direction; float length;
                if(!Span(hand,other,out direction,out length)) return;
                target=item; primary=hand; ActiveSecondary=other; clutchActive=true; owner=hand.UserId; parent=item.transform.parent;
                parentFrame=parent!=null?parent.localToWorldMatrix:Matrix4x4.identity; regionFrame=item.Region.transform.localToWorldMatrix;
                startDirection=direction; startSpan=length; startFactor=interactor.CurrentScaleFactor;
                startWorldRotation=BirdPlacementRegion.AuthoredRotation(parent)*item.transform.localRotation;
                if(!interactor.TrySetHeldPose(item.transform.localRotation,startFactor)) { End(false,true); return; }
                RememberRequest(); engaged.Invoke();
            }
            finally { processing=false; }
        }
        bool Held()
        {
            return target!=null && primary!=null && ActiveSecondary!=null && target.isActiveAndEnabled && interactor.ActiveTarget==target && interactor.ActivePointer==primary &&
                target.Owner==interactor && !interactor.IsReturning && target.Region!=null && target.Region.isActiveAndEnabled &&
                target.transform.parent==parent && (parent!=null?parent.localToWorldMatrix:Matrix4x4.identity)==parentFrame &&
                target.Region.transform.localToWorldMatrix==regionFrame && primary.UserId==owner && ActiveSecondary.UserId==owner;
        }
        void Apply()
        {
            Vector3 direction; float length;
            if(!Span(primary,ActiveSecondary,out direction,out length) || Vector3.Angle(startDirection,direction)>maximumTurnDegrees)
            { End(true,true); return; }
            Quaternion orientation=target.AllowRotation?Quaternion.Inverse(BirdPlacementRegion.AuthoredRotation(parent))*Quaternion.FromToRotation(startDirection,direction)*startWorldRotation:target.transform.localRotation;
            float factor=target.AllowScaling?(float)((double)startFactor*length/startSpan):startFactor;
            bool accepted=interactor.TrySetHeldPose(orientation,factor);
            GestureLimited=!accepted; RememberRequest();
        }
        void RememberRequest()
        {
            lastRequest=interactor.RequestedLocalRotation; lastFactor=interactor.RequestedScaleFactor;
            appliedFirst=firstRevision; appliedSecond=secondRevision;
        }
        void End(bool freeze,bool limited)
        {
            bool notify=IsEngaged;
            var grip=boundInteractor;
            if(freeze && notify && grip!=null && grip.ActiveTarget==target && !grip.IsReturning) grip.StopHeldPose(limited);
            clutchActive=false; ActiveSecondary=null; target=null; primary=null; GestureLimited=limited;
            if(notify) disengaged.Invoke();
        }
        bool Span(BirdPointerInput a,BirdPointerInput b,out Vector3 direction,out float length)
        {
            Vector3 delta=b.Origin-a.Origin; length=delta.magnitude; direction=Vector3.zero;
            if(!BirdPlacementRegion.Finite(delta) || !BirdPlacementRegion.Finite(length) || length<minimumSpan || length>maximumSpan) return false;
            direction=delta/length; return true;
        }
        bool ValidSettings()
        {
            return BirdPlacementRegion.Finite(minimumSpan) && BirdPlacementRegion.Finite(maximumSpan) && minimumSpan>0 && maximumSpan>=minimumSpan &&
                BirdPlacementRegion.Finite(maximumSampleGap) && maximumSampleGap>0 && maximumSampleGap<=.25f &&
                BirdPlacementRegion.Finite(maximumTurnDegrees) && maximumTurnDegrees>=30 && maximumTurnDegrees<=175;
        }
        static bool Tracked(BirdPointerInput value) { return value!=null && value.IsTracked && value.HasMotionHistory; }
        static bool Hit(BoxCollider volume,BirdPointerInput pointer)
        {
            if(volume==null || !volume.enabled || !volume.gameObject.activeInHierarchy) return false;
            if((volume.ClosestPoint(pointer.Origin)-pointer.Origin).sqrMagnitude<1e-10f) return true;
            Vector3 delta=pointer.Position-pointer.Origin; float length=delta.magnitude; RaycastHit hit;
            return length>1e-6f && volume.Raycast(new Ray(pointer.Origin,delta/length),out hit,length);
        }
    }
}
