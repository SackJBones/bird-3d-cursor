using UdonSharp;
using UnityEngine;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdObjectTarget : UdonSharpBehaviour
{
    public BoxCollider volume;
    public BirdObjectRegion region;
    public BirdObjectPolicy policy;
    public BirdObjectSnapTarget[] destinations=new BirdObjectSnapTarget[0];
    public UdonBehaviour eventTarget;
    public string grabbedEvent,placedEvent,cancelledEvent;
    [HideInInspector] public BirdObjectGrip owner;
    public bool Available()
    {
        if(!enabled || !gameObject.activeInHierarchy || volume==null || !volume.enabled || !volume.gameObject.activeInHierarchy || region==null || !region.enabled || !region.gameObject.activeInHierarchy) return false;
        Rigidbody body=volume.attachedRigidbody;
        return (body==null || body.isKinematic) && (policy==null || policy.Query(BirdObjectOperation.CanGrab,this,null));
    }
    public void Notify(int kind)
    {
        if(eventTarget==null) return;
        string name=kind==0?grabbedEvent:kind==1?placedEvent:cancelledEvent;
        if(!string.IsNullOrEmpty(name)) eventTarget.SendCustomEvent(name);
    }
    private void OnDisable() { if(owner!=null) owner.CancelImmediately(); }
}
