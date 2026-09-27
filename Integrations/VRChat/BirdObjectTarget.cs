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
    [Header("Optional held pose")]
    public bool allowRotation,allowScaling;
    [Tooltip("Authored local scale at factor 1, preserved across placements.")]
    public Vector3 poseReferenceScale=Vector3.one;
    [Min(.0001f)] public float minimumScaleFactor=.5f,maximumScaleFactor=2;
    [HideInInspector] public float evaluatedScaleFactor=1;
    public UdonBehaviour eventTarget;
    public string grabbedEvent,placedEvent,cancelledEvent;
    [HideInInspector] public BirdObjectGrip owner;
    public void CapturePoseReference() { if(owner==null) poseReferenceScale=transform.localScale; }
    public bool TryScaleFactor(Vector3 value)
    {
        evaluatedScaleFactor=1;
        if(!FiniteVector(poseReferenceScale) || !FiniteVector(value) || Mathf.Abs(poseReferenceScale.x*poseReferenceScale.y*poseReferenceScale.z)<1e-12f) return false;
        Vector3 ratios=new Vector3(value.x/poseReferenceScale.x,value.y/poseReferenceScale.y,value.z/poseReferenceScale.z);
        evaluatedScaleFactor=ratios.x;
        return FiniteVector(ratios) && evaluatedScaleFactor>0 && Mathf.Abs(ratios.y-evaluatedScaleFactor)<evaluatedScaleFactor*.0001f && Mathf.Abs(ratios.z-evaluatedScaleFactor)<evaluatedScaleFactor*.0001f;
    }
    public bool AllowsFactor(float factor)
    {
        return Finite(factor) && Finite(minimumScaleFactor) && Finite(maximumScaleFactor) && minimumScaleFactor>0 && maximumScaleFactor>=minimumScaleFactor && factor>=minimumScaleFactor && factor<=maximumScaleFactor;
    }
    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x)&&Finite(value.y)&&Finite(value.z); }
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
