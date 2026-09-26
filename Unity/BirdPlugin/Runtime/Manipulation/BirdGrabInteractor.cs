using System;
using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor.UI;

namespace Bird3DCursor.Manipulation
{
    /// <summary>One local kinematic transaction, with optional constrained rotation and uniform scaling.</summary>
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
        public Quaternion RequestedLocalRotation { get; private set; }=Quaternion.identity;
        public float RequestedScaleFactor { get; private set; }=1;
        public float CurrentScaleFactor { get; private set; }=1;
        public bool PoseLimited { get; private set; }
        readonly Dictionary<BirdPointerInput,uint> revisions=new Dictionary<BirdPointerInput,uint>();
        BirdPlacementRegion region;
        BirdPlacementRule rule;
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
                    AdvancePose(dt);
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
            float factor=1;
            bool knownScale=item.TryScaleFactor(item.transform.localScale,out factor);
            if(item.AllowScaling && (!knownScale || !item.AllowsFactor(factor))) return;
            if(!knownScale) factor=1;
            Vector3 start=item.Region.transform.InverseTransformPoint(item.transform.position);
            if((BirdPlacementRegion.Clamp(bounds,start)-start).sqrMagnitude>1e-8f) return;
            if(item.Rule!=null && !item.Rule.TryBegin(item)) return;
            ActiveTarget=item; ActivePointer=pointer; region=item.Region; rule=item.Rule; owner=pointer.UserId;
            home=previousDesired=start; anchor=region.transform.InverseTransformPoint(pointer.Position);
            parent=item.transform.parent; rotation=item.transform.localRotation; scale=item.transform.localScale;
            homeRotation=RequestedLocalRotation=rotation; homeScale=scale;
            homeFactor=CurrentScaleFactor=RequestedScaleFactor=factor; PoseLimited=false;
            poseStartRotation=rotation; poseStartLogScale=Math.Log(factor); poseResponseIntegral=0;
            poseRotation=item.AllowRotation; poseScaling=item.AllowScaling; referenceScale=item.PoseReferenceScale;
            hasReferenceScale=knownScale;
            minimumFactor=item.MinimumScaleFactor; maximumFactor=item.MaximumScaleFactor;
            frame=region.transform.localToWorldMatrix; parentFrame=parent!=null?parent.localToWorldMatrix:Matrix4x4.identity; allowed=bounds;
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
                (parent!=null?parent.localToWorldMatrix:Matrix4x4.identity)==parentFrame &&
                t.AllowRotation==poseRotation && t.AllowScaling==poseScaling && t.PoseReferenceScale==referenceScale &&
                t.MinimumScaleFactor==minimumFactor && t.MaximumScaleFactor==maximumFactor &&
                t.Volume.center==shapeCenter && t.Volume.size==shapeSize &&
                (t.Volume.attachedRigidbody==null || t.Volume.attachedRigidbody.isKinematic) &&
                region.TryPivotBounds(t.transform,t.Volume,out currentBounds) &&
                (currentBounds.min-allowed.min).sqrMagnitude<1e-7f && (currentBounds.max-allowed.max).sqrMagnitude<1e-7f;
        }
        /// <summary>Request an absolute local orientation and authored-reference scale factor. Does not mutate the target until Process.</summary>
        public bool TrySetHeldPose(Quaternion localRotation,float factor)
        {
            Quaternion unit; Bounds bounds;
            if(!isActiveAndEnabled || ActiveTarget==null || IsReturning) return false;
            if(!BirdPlacementRegion.Normalize(localRotation,out unit) || !PoseAllowed(unit,factor,out bounds))
            { StopPoseRequest(); return false; }
            if(unit.x!=RequestedLocalRotation.x || unit.y!=RequestedLocalRotation.y || unit.z!=RequestedLocalRotation.z || unit.w!=RequestedLocalRotation.w || factor!=RequestedScaleFactor)
            { poseStartRotation=rotation; poseStartLogScale=Math.Log(CurrentScaleFactor); poseResponseIntegral=0; }
            RequestedLocalRotation=unit; RequestedScaleFactor=factor; PoseLimited=false; return true;
        }
        public void ResetHeldPose() { TrySetHeldPose(homeRotation,homeFactor); }
        bool PoseAllowed(Quaternion orientation,float factor,out Bounds bounds)
        {
            bounds=new Bounds();
            if(ActiveTarget==null || region==null || !BirdPlacementRegion.Finite(factor) || factor<=0 ||
                (!poseRotation && Quaternion.Angle(orientation,homeRotation)>.001f) ||
                (poseScaling?!ActiveTarget.AllowsFactor(factor):Mathf.Abs(factor-homeFactor)>1e-6f)) return false;
            return region.TryPivotBounds(ActiveTarget.transform,ActiveTarget.Volume,orientation,poseScaling?referenceScale*factor:homeScale,out bounds);
        }
        void AdvancePose(float dt)
        {
            if(!poseRotation && !poseScaling) return;
            float rate=BirdPlacementRegion.Finite(followRate)?Mathf.Max(.01f,followRate):18;
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
        bool DestinationPose(BirdSnapTarget slot,out Quaternion orientation,out float factor,out Bounds bounds)
        {
            orientation=rotation; factor=CurrentScaleFactor; bounds=allowed;
            if(PoseLimited) return false;
            if(slot.matchRotation)
            {
                if(!BirdPlacementRegion.Finite(slot.rotationCaptureDegrees) || slot.rotationCaptureDegrees<0 || slot.rotationCaptureDegrees>180) return false;
                Quaternion desired=parent!=null?Quaternion.Inverse(parent.rotation)*slot.transform.rotation:slot.transform.rotation;
                if(!BirdPlacementRegion.Normalize(desired,out orientation) ||
                    Quaternion.Angle(RequestedLocalRotation,orientation)>slot.rotationCaptureDegrees ||
                    Quaternion.Angle(rotation,orientation)>slot.rotationCaptureDegrees) return false;
            }
            if(slot.matchScale)
            {
                if(!hasReferenceScale) return false;
                factor=slot.scaleFactor;
                if(!BirdPlacementRegion.Finite(factor) || factor<=0 || !BirdPlacementRegion.Finite(slot.scaleCaptureRatio) || slot.scaleCaptureRatio<0) return false;
                float tolerance=Mathf.Log(1+slot.scaleCaptureRatio);
                if(Mathf.Abs(Mathf.Log(RequestedScaleFactor/factor))>tolerance || Mathf.Abs(Mathf.Log(CurrentScaleFactor/factor))>tolerance) return false;
            }
            return PoseAllowed(orientation,factor,out bounds);
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
                Quaternion finalRotation; float finalFactor; Bounds finalBounds;
                ReadyToPlace=Vector3.Distance(raw,end)<=Candidate.captureRadius &&
                    DestinationPose(Candidate,out finalRotation,out finalFactor,out finalBounds) &&
                    (BirdPlacementRegion.Clamp(finalBounds,end)-end).sqrMagnitude<1e-8f;
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
            Quaternion finalRotation; float finalFactor; Bounds finalBounds;
            if(!ReadyToPlace || slot==null || !Eligible(slot) || !DestinationPose(slot,out finalRotation,out finalFactor,out finalBounds) ||
                (rule!=null && !rule.TryCommit(item,slot))) { Cancel(); return; }
            if(poseRotation) item.transform.localRotation=finalRotation;
            if(poseScaling) item.transform.localScale=referenceScale*finalFactor;
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
            returnRotation=item.transform.localRotation; returnScale=item.transform.localScale;
            item.Cancelled.Invoke();
        }
        void AdvanceReturn(float dt)
        {
            if(region==null) { Clear(); return; }
            returnElapsed+=dt;
            float duration=BirdPlacementRegion.Finite(returnDuration)?Mathf.Max(.01f,returnDuration):.25f;
            float t=Mathf.Clamp01(returnElapsed/duration);
            float weight=t*t*(3-2*t);
            Vector3 point=Vector3.Lerp(returnStart,home,weight);
            if(poseRotation || poseScaling)
            {
                Quaternion turn=poseRotation?Quaternion.Slerp(returnRotation,homeRotation,weight):ActiveTarget.transform.localRotation;
                Vector3 size=poseScaling?Vector3.Lerp(returnScale,homeScale,weight):ActiveTarget.transform.localScale;
                Bounds bounds;
                if(!region.TryPivotBounds(ActiveTarget.transform,ActiveTarget.Volume,turn,size,out bounds))
                { RestoreHomePose(); Move(home); Clear(); return; }
                ActiveTarget.transform.localRotation=turn; ActiveTarget.transform.localScale=size;
                point=BirdPlacementRegion.Clamp(bounds,point);
            }
            Move(point);
            if(t>=1) Clear();
        }
        public void CancelImmediately()
        {
            Seed();
            if(ActiveTarget==null) { Clear(); return; }
            var item=ActiveTarget; bool notify=!IsReturning;
            if(rule!=null) rule.Cancel(item);
            RestoreHomePose(); Move(home); Clear(); if(notify) item.Cancelled.Invoke();
        }
        void RestoreHomePose()
        {
            if(ActiveTarget==null) return;
            if(poseRotation) ActiveTarget.transform.localRotation=homeRotation;
            if(poseScaling) ActiveTarget.transform.localScale=homeScale;
        }
        void Clear()
        {
            if(ActiveTarget!=null && ActiveTarget.Owner==this) ActiveTarget.Owner=null;
            ActiveTarget=null; ActivePointer=null; Candidate=null; HoveredTarget=null; ReadyToPlace=false; IsReturning=false; region=null; rule=null;
            RequestedLocalRotation=Quaternion.identity; RequestedScaleFactor=CurrentScaleFactor=1; PoseLimited=false;
        }
    }
}
