using System;
using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor.UI;

namespace Bird3DCursor.Manipulation
{
    /// <summary>One local transaction at a time, selected by either hand. Translation only; kinematic volumes.</summary>
    [AddComponentMenu("Bird/Manipulation/Grab Interactor")]
    [DefaultExecutionOrder(120)]
    public sealed class BirdGrabInteractor : MonoBehaviour
    {
        [SerializeField] BirdPointerInput[] pointers=new BirdPointerInput[0];
        [SerializeField] BirdGrabTarget[] targets=new BirdGrabTarget[0];
        [Min(.01f)] public float followRate=18;
        [Min(.01f)] public float returnDuration=.25f;
        public bool automatic=true;
        public BirdGrabTarget ActiveTarget { get; private set; }
        public BirdPointerInput ActivePointer { get; private set; }
        public BirdSnapTarget Candidate { get; private set; }
        public BirdGrabTarget HoveredTarget { get; private set; }
        public bool ReadyToPlace { get; private set; }
        public bool IsReturning { get; private set; }
        public Vector3 RawDesired { get; private set; }
        readonly Dictionary<BirdPointerInput,uint> revisions=new Dictionary<BirdPointerInput,uint>();
        BirdPlacementRegion region;
        BirdPlacementRule rule;
        string owner;
        Vector3 home,anchor,previousDesired,returnStart,shapeCenter,shapeSize;
        Quaternion rotation;
        Vector3 scale;
        Transform parent;
        Matrix4x4 frame;
        Bounds allowed;
        float returnElapsed;
        bool processing;

        public void Configure(BirdPointerInput[] inputs,BirdGrabTarget[] items)
        {
            if(processing) throw new InvalidOperationException("Configure between interaction updates.");
            CancelImmediately(); pointers=inputs!=null?(BirdPointerInput[])inputs.Clone():new BirdPointerInput[0];
            targets=items!=null?(BirdGrabTarget[])items.Clone():new BirdGrabTarget[0]; Seed();
        }
        void OnEnable() { Seed(); }
        void Seed() { revisions.Clear(); foreach(var p in pointers) if(p!=null) revisions[p]=p.Revision; }
        void OnDisable() { CancelImmediately(); Seed(); }
        void LateUpdate() { if(automatic) Process(Time.deltaTime); }

        public void Process(float dt)
        {
            if(processing || !isActiveAndEnabled) return;
            processing=true;
            try
            {
                if(!BirdPlacementRegion.Finite(dt) || dt<=0) { CancelImmediately(); Seed(); return; }
                if(ActiveTarget!=null)
                {
                    HoveredTarget=null;
                    if(IsReturning) { AdvanceReturn(Mathf.Min(dt,.25f)); Seed(); return; }
                    var p=ActivePointer;
                    if(dt>.25f || p==null || !p.IsTracked || !p.HasMotionHistory || p.UserId!=owner || !ValidHeld())
                    { Cancel(); Seed(); return; }
                    Vector3 raw=home+(region.transform.InverseTransformPoint(p.Position)-anchor);
                    if(!BirdPlacementRegion.Finite(raw)) { Cancel(); Seed(); return; }
                    RawDesired=raw;
                    Vector3 desired=Guide(raw);
                    float rate=BirdPlacementRegion.Finite(followRate)?Mathf.Max(.01f,followRate):18;
                    Vector3 current=region.transform.InverseTransformPoint(ActiveTarget.transform.position);
                    // Exact first-order response to a linearly moving target during this step.
                    float interval=rate*dt;
                    float decay=Mathf.Exp(-interval);
                    float response=interval<.001f?interval*(1-interval*.5f+interval*interval/6):1-decay;
                    float ramp=interval<.001f?interval*(.5f-interval/6+interval*interval/24):1-response/interval;
                    Vector3 next=current+(previousDesired-current)*response+(desired-previousDesired)*ramp;
                    previousDesired=desired;
                    Move(BirdPlacementRegion.Clamp(allowed,next));
                    if(ReadyToPlace) ReadyToPlace=Vector3.Distance(region.transform.InverseTransformPoint(ActiveTarget.transform.position),region.transform.InverseTransformPoint(Candidate.transform.position))<=Candidate.captureRadius*1.5f;
                    if(!p.IsPressed) FinishDrop();
                    Seed(); return;
                }
                // Consume all revisions before callbacks can re-enter or reconfigure input.
                BirdGrabTarget best=null; BirdPointerInput hand=null;
                float nearest=float.PositiveInfinity,hoverDistance=float.PositiveInfinity; HoveredTarget=null;
                foreach(var p in pointers)
                {
                    if(p==null) continue;
                    uint rev; bool fresh=!revisions.TryGetValue(p,out rev)||rev!=p.Revision;
                    revisions[p]=p.Revision;
                    if(!p.IsTracked) continue;
                    foreach(var item in targets)
                    {
                        if(item==null || item.Owner!=null || !item.Available()) continue;
                        float distance;
                        if(!Hit(item.Volume,p,out distance)) continue;
                        if(distance<hoverDistance) { HoveredTarget=item; hoverDistance=distance; }
                        if(fresh && p.PressedThisSample && distance<nearest) { best=item; hand=p; nearest=distance; }
                    }
                }
                if(best!=null) Begin(best,hand);
            }
            finally { processing=false; }
        }

        static bool Hit(BoxCollider volume,BirdPointerInput pointer,out float distance)
        {
            distance=0;
            if((volume.ClosestPoint(pointer.Origin)-pointer.Origin).sqrMagnitude<1e-10f) return true;
            Vector3 delta=pointer.Position-pointer.Origin; float length=delta.magnitude;
            RaycastHit hit;
            if(length<1e-6f) return (volume.ClosestPoint(pointer.Position)-pointer.Position).sqrMagnitude<1e-10f;
            if(!volume.Raycast(new Ray(pointer.Origin,delta/length),out hit,length)) return false;
            distance=hit.distance; return true;
        }
        void Begin(BirdGrabTarget item,BirdPointerInput pointer)
        {
            Bounds bounds;
            if(!item.Region.TryPivotBounds(item.transform,item.Volume,out bounds)) return;
            Vector3 start=item.Region.transform.InverseTransformPoint(item.transform.position);
            if((BirdPlacementRegion.Clamp(bounds,start)-start).sqrMagnitude>1e-8f) return;
            if(item.Rule!=null && !item.Rule.TryBegin(item)) return;
            ActiveTarget=item; ActivePointer=pointer; region=item.Region; rule=item.Rule; owner=pointer.UserId;
            home=previousDesired=start; anchor=region.transform.InverseTransformPoint(pointer.Position);
            parent=item.transform.parent; rotation=item.transform.localRotation; scale=item.transform.localScale;
            frame=region.transform.localToWorldMatrix; allowed=bounds;
            shapeCenter=item.Volume.center; shapeSize=item.Volume.size;
            RawDesired=home; Candidate=null; ReadyToPlace=false; IsReturning=false; item.Owner=this;
            item.Grabbed.Invoke();
        }
        bool ValidHeld()
        {
            var t=ActiveTarget;
            Bounds currentBounds;
            return t!=null && t.isActiveAndEnabled && t.Volume!=null && t.Volume.enabled && t.Volume.gameObject.activeInHierarchy &&
                region!=null && region.isActiveAndEnabled && t.Region==region && t.Rule==rule &&
                (rule==null || rule.isActiveAndEnabled) && region.transform.localToWorldMatrix==frame &&
                t.transform.parent==parent && t.transform.localRotation==rotation && t.transform.localScale==scale &&
                t.Volume.center==shapeCenter && t.Volume.size==shapeSize &&
                (t.Volume.attachedRigidbody==null || t.Volume.attachedRigidbody.isKinematic) &&
                region.TryPivotBounds(t.transform,t.Volume,out currentBounds) &&
                (currentBounds.min-allowed.min).sqrMagnitude<1e-7f && (currentBounds.max-allowed.max).sqrMagnitude<1e-7f;
        }
        Vector3 Guide(Vector3 raw)
        {
            Vector3 end,guided; float distance;
            if(Candidate==null || !Eligible(Candidate) || !Candidate.Evaluate(region,raw,out end,out guided,out distance) ||
                distance>Candidate.influenceRadius*Candidate.exitMultiplier) Candidate=null;
            if(Candidate==null)
            {
                float best=float.PositiveInfinity;
                foreach(var slot in ActiveTarget.Destinations)
                {
                    if(slot==null || !Eligible(slot) || !slot.Evaluate(region,raw,out end,out guided,out distance)) continue;
                    float normalized=distance/slot.influenceRadius;
                    if(normalized<=1 && normalized<best) { best=normalized; Candidate=slot; }
                }
            }
            ReadyToPlace=false;
            Vector3 desired=raw;
            if(Candidate!=null && Candidate.Evaluate(region,raw,out end,out guided,out distance))
            {
                desired=guided;
                // Never qualify a drop using the clamped or magnetically guided pose.
                ReadyToPlace=Vector3.Distance(raw,end)<=Candidate.captureRadius &&
                    (BirdPlacementRegion.Clamp(allowed,end)-end).sqrMagnitude<1e-8f;
            }
            return BirdPlacementRegion.Clamp(allowed,desired);
        }
        bool Eligible(BirdSnapTarget slot)
        {
            return slot.isActiveAndEnabled && Array.IndexOf(ActiveTarget.Destinations,slot)>=0 &&
                (rule==null || (rule.isActiveAndEnabled && rule.CanPlace(ActiveTarget,slot)));
        }
        void FinishDrop()
        {
            var item=ActiveTarget; var slot=Candidate;
            if(!ReadyToPlace || slot==null || !Eligible(slot) || (rule!=null && !rule.TryCommit(item,slot))) { Cancel(); return; }
            Move(region.transform.InverseTransformPoint(slot.transform.position));
            Clear(); item.Placed.Invoke();
        }
        void Move(Vector3 point)
        {
            if(ActiveTarget==null || region==null) return;
            ActiveTarget.transform.position=region.transform.TransformPoint(point); Physics.SyncTransforms();
        }
        public void Cancel()
        {
            Seed();
            if(ActiveTarget==null || IsReturning) return;
            var item=ActiveTarget;
            if(rule!=null) rule.Cancel(item);
            Candidate=null; ReadyToPlace=false; ActivePointer=null; IsReturning=true; returnElapsed=0;
            returnStart=region!=null?region.transform.InverseTransformPoint(item.transform.position):home;
            item.Cancelled.Invoke();
        }
        void AdvanceReturn(float dt)
        {
            if(region==null) { Clear(); return; }
            returnElapsed+=dt;
            float duration=BirdPlacementRegion.Finite(returnDuration)?Mathf.Max(.01f,returnDuration):.25f;
            float t=Mathf.Clamp01(returnElapsed/duration);
            Move(Vector3.Lerp(returnStart,home,t*t*(3-2*t)));
            if(t>=1) Clear();
        }
        public void CancelImmediately()
        {
            Seed();
            if(ActiveTarget==null) { Clear(); return; }
            var item=ActiveTarget; bool notify=!IsReturning;
            if(rule!=null) rule.Cancel(item);
            Move(home); Clear(); if(notify) item.Cancelled.Invoke();
        }
        void Clear()
        {
            if(ActiveTarget!=null && ActiveTarget.Owner==this) ActiveTarget.Owner=null;
            ActiveTarget=null; ActivePointer=null; Candidate=null; HoveredTarget=null; ReadyToPlace=false; IsReturning=false; region=null; rule=null;
        }
    }
}
