using UnityEngine;
using UnityEngine.Events;

namespace Bird3DCursor.Manipulation
{
    [AddComponentMenu("Bird/Manipulation/Grab Target")]
    [DisallowMultipleComponent]
    public sealed class BirdGrabTarget : MonoBehaviour
    {
        [SerializeField] BoxCollider volume;
        [SerializeField] BirdPlacementRegion region;
        [SerializeField] BirdPlacementRule rule;
        [SerializeField] BirdSnapTarget[] destinations=new BirdSnapTarget[0];
        [Header("Optional held pose")]
        [SerializeField] bool allowRotation;
        [SerializeField] bool allowScaling;
        [Tooltip("Authored local scale at factor 1. Capture after sizing the object; preserved across placements.")]
        [SerializeField] Vector3 poseReferenceScale=Vector3.one;
        [Min(.0001f)] [SerializeField] float minimumScaleFactor=.5f;
        [Min(.0001f)] [SerializeField] float maximumScaleFactor=2;
        [SerializeField] UnityEvent grabbed=new UnityEvent();
        [SerializeField] UnityEvent placed=new UnityEvent();
        [SerializeField] UnityEvent cancelled=new UnityEvent();
        public BoxCollider Volume { get { return volume; } }
        public BirdPlacementRegion Region { get { return region; } }
        public BirdPlacementRule Rule { get { return rule; } }
        public BirdSnapTarget[] Destinations { get { return destinations; } }
        public UnityEvent Grabbed { get { return grabbed; } }
        public UnityEvent Placed { get { return placed; } }
        public UnityEvent Cancelled { get { return cancelled; } }
        public BirdGrabInteractor Owner { get; internal set; }
        public bool AllowRotation { get { return allowRotation; } }
        public bool AllowScaling { get { return allowScaling; } }
        public Vector3 PoseReferenceScale { get { return poseReferenceScale; } }
        public float MinimumScaleFactor { get { return minimumScaleFactor; } }
        public float MaximumScaleFactor { get { return maximumScaleFactor; } }

        public void ConfigurePose(bool rotate,bool resize,float minimum=.5f,float maximum=2)
        {
            if(Owner!=null) throw new System.InvalidOperationException("Configure pose limits between transactions.");
            if(!BirdPlacementRegion.Finite(minimum) || !BirdPlacementRegion.Finite(maximum) || minimum<=0 || minimum>1 || maximum<1)
                throw new System.ArgumentOutOfRangeException("Scale limits must be finite, positive and contain factor 1.");
            allowRotation=rotate; allowScaling=resize; minimumScaleFactor=minimum; maximumScaleFactor=maximum; CapturePoseReference();
        }
        [ContextMenu("Capture Pose Reference Scale")]
        public void CapturePoseReference()
        {
            if(Owner!=null) throw new System.InvalidOperationException("Capture the authored scale between transactions.");
            poseReferenceScale=transform.localScale;
        }
        internal bool TryScaleFactor(Vector3 value,out float factor)
        {
            factor=1;
            if(!BirdPlacementRegion.Finite(poseReferenceScale) || !BirdPlacementRegion.Finite(value) ||
                Mathf.Abs(poseReferenceScale.x*poseReferenceScale.y*poseReferenceScale.z)<1e-12f) return false;
            Vector3 ratios=new Vector3(value.x/poseReferenceScale.x,value.y/poseReferenceScale.y,value.z/poseReferenceScale.z);
            factor=ratios.x;
            return BirdPlacementRegion.Finite(ratios) && factor>0 &&
                Mathf.Abs(ratios.y-factor)<factor*.0001f && Mathf.Abs(ratios.z-factor)<factor*.0001f;
        }
        internal bool AllowsFactor(float factor)
        {
            return BirdPlacementRegion.Finite(factor) && BirdPlacementRegion.Finite(minimumScaleFactor) && BirdPlacementRegion.Finite(maximumScaleFactor) &&
                minimumScaleFactor>0 && maximumScaleFactor>=minimumScaleFactor && factor>=minimumScaleFactor && factor<=maximumScaleFactor;
        }

        public void Configure(BoxCollider shape,BirdPlacementRegion workspace,BirdSnapTarget[] slots,BirdPlacementRule policy=null)
        {
            if(Owner!=null) Owner.CancelImmediately();
            volume=shape; region=workspace; destinations=slots!=null?(BirdSnapTarget[])slots.Clone():new BirdSnapTarget[0]; rule=policy;
        }
        internal bool Available()
        {
            if(!isActiveAndEnabled || volume==null || !volume.enabled || !volume.gameObject.activeInHierarchy || region==null || !region.isActiveAndEnabled) return false;
            var body=volume.attachedRigidbody;
            return (body==null || body.isKinematic) && (rule==null || (rule.isActiveAndEnabled && rule.CanGrab(this)));
        }
        void Reset() { volume=GetComponent<BoxCollider>(); region=GetComponentInParent<BirdPlacementRegion>(); }
        void OnDisable() { if(Owner!=null) Owner.CancelImmediately(); }
    }
}
