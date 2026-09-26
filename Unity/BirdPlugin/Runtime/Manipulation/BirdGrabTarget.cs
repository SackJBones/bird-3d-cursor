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
