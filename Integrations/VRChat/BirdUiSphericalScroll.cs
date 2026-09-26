using System;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

// Udon counterpart of BirdSphericalScroll. Logical far-surface contact, never a display proxy.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(110)]
public class BirdUiSphericalScroll : UdonSharpBehaviour
{
    public SphereCollider sphere;
    public Transform rotationTarget;
    public BirdUiPointer[] pointers=new BirdUiPointer[0];
    public BirdUiPanel panel;
    [Min(0)] public float entryMargin=.003f;
    [Min(0)] public float response=10;
    [Min(0)] public float damping=.99f;
    [Min(1)] public float maximumSpeed=720;
    public UdonBehaviour eventTarget;
    public string startedEvent, stoppedEvent;
    public bool automatic=true;
    [HideInInspector] public float stepDelta=.01666667f;
    [HideInInspector] public BirdUiPointer activePointer;
    [HideInInspector] public Vector3 angularVelocity, contactNormal, contactPoint;
    private BirdUiPointer motionOwner;
    private string ownerId;
    private Vector3 previousNormal;
    private int revision;
    private bool stepping, hasMotionOwner;

    private void LateUpdate() { if(automatic) { stepDelta=Time.unscaledDeltaTime; Process(); } }
    private void OnDisable() { Cancel(); }
    public void Cancel()
    {
        bool wasActive=activePointer!=null;
        activePointer=motionOwner=null; hasMotionOwner=false; ownerId=null; angularVelocity=Vector3.zero;
        if(wasActive && eventTarget!=null && !string.IsNullOrEmpty(stoppedEvent)) eventTarget.SendCustomEvent(stoppedEvent);
    }
    private bool Eligible(BirdUiPointer pointer)
    {
        return pointer!=null && pointer.IsTracked() && (panel==null || (panel.Accepts(pointer) && panel.state==1));
    }
    public void Process()
    {
        if(stepping || !enabled || !gameObject.activeInHierarchy) return;
        stepping=true;
        Step();
        stepping=false;
    }
    private void Step()
    {
        float dt=stepDelta;
        if(!Finite(dt) || dt<=0 || dt>.25f || !ValidSphere() || rotationTarget==null || !rotationTarget.gameObject.activeInHierarchy ||
            !Finite(response) || !Finite(damping) || !Finite(maximumSpeed) || response<0 || damping<0 || maximumSpeed<=0)
        { Cancel(); return; }
        if(hasMotionOwner && (!Eligible(motionOwner) || motionOwner.userId!=ownerId)) Cancel();
        if(!enabled || !gameObject.activeInHierarchy) return;
        bool driving=Eligible(activePointer) && Contact(activePointer,0);
        if(!driving && activePointer!=null)
        {
            activePointer=null;
            if(eventTarget!=null && !string.IsNullOrEmpty(stoppedEvent)) eventTarget.SendCustomEvent(stoppedEvent);
            if(!enabled || !gameObject.activeInHierarchy) return;
        }
        bool acquired=false;
        if(activePointer==null)
        {
            foreach(BirdUiPointer pointer in pointers)
            {
                if(!Eligible(pointer) || !Contact(pointer,entryMargin)) continue;
                if(motionOwner!=null && motionOwner!=pointer) angularVelocity=Vector3.zero;
                activePointer=motionOwner=pointer; ownerId=pointer.userId; hasMotionOwner=true;
                previousNormal=contactNormal; revision=pointer.revision; acquired=true;
                if(eventTarget!=null && !string.IsNullOrEmpty(startedEvent)) eventTarget.SendCustomEvent(startedEvent);
                break;
            }
        }
        if(!enabled || !gameObject.activeInHierarchy || rotationTarget==null) return;
        Vector3 inputVelocity=Vector3.zero;
        if(activePointer!=null)
        {
            if(!Eligible(activePointer) || !Contact(activePointer,0)) { Cancel(); return; }
            if(!acquired && revision!=activePointer.revision)
            {
                Vector3 cross=Vector3.Cross(previousNormal,contactNormal);
                float sine=cross.magnitude, cosine=Mathf.Clamp(Vector3.Dot(previousNormal,contactNormal),-1,1);
                if(!activePointer.hasHistory) angularVelocity=Vector3.zero;
                else if(sine>1e-6f) inputVelocity=cross/sine*Mathf.Min(Mathf.Atan2(sine,cosine)/dt,maximumSpeed*Mathf.Deg2Rad);
                previousNormal=contactNormal; revision=activePointer.revision;
            }
        }
        float rate=damping+(activePointer!=null ? response : 0);
        Vector3 equilibrium=rate>0 && activePointer!=null ? inputVelocity*(response/rate) : Vector3.zero;
        float decay=Mathf.Exp(-rate*dt);
        double x=-(double)rate*dt;
        double expm1=Math.Abs(x)<1e-5 ? x*(1+x*(.5+x/6)) : Math.Exp(x)-1;
        float integral=rate>.0001f ? (float)(-expm1/rate) : dt;
        Vector3 rotation=equilibrium*dt+(angularVelocity-equilibrium)*integral;
        angularVelocity=equilibrium+(angularVelocity-equilibrium)*decay;
        float angle=rotation.magnitude;
        if(angle>1e-8f)
        {
            rotationTarget.RotateAround(sphere.transform.TransformPoint(sphere.center),rotation/angle,angle*Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }
    }
    public bool Contact(BirdUiPointer pointer, float margin)
    {
        contactNormal=contactPoint=Vector3.zero;
        if(!ValidSphere() || pointer==null || !pointer.IsTracked() || !Finite(margin) || margin<0) return false;
        Vector3 center=sphere.transform.TransformPoint(sphere.center), scale=sphere.transform.lossyScale;
        double radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
        Vector3 origin=pointer.origin, point=pointer.position;
        double dx=(double)point.x-origin.x, dy=(double)point.y-origin.y, dz=(double)point.z-origin.z;
        double length=Math.Sqrt(dx*dx+dy*dy+dz*dz);
        if(length<=1e-7) return false;
        dx/=length; dy/=length; dz/=length;
        double cx=(double)center.x-origin.x, cy=(double)center.y-origin.y, cz=(double)center.z-origin.z;
        double along=cx*dx+cy*dy+cz*dz;
        double px=cx-along*dx, py=cy-along*dy, pz=cz-along*dz;
        double radicand=radius*radius-(px*px+py*py+pz*pz);
        if(radicand<=radius*radius*1e-12) return false;
        double beyond=Math.Sqrt(radicand), far=along+beyond;
        if(far<=0 || length<=far+margin) return false;
        contactNormal=new Vector3((float)((beyond*dx-px)/radius),(float)((beyond*dy-py)/radius),(float)((beyond*dz-pz)/radius)).normalized;
        contactPoint=center+contactNormal*(float)radius;
        return FiniteVector(contactNormal) && FiniteVector(contactPoint) && contactNormal.sqrMagnitude>.5f;
    }
    private bool ValidSphere()
    {
        if(sphere==null || !sphere.enabled || !sphere.gameObject.activeInHierarchy) return false;
        Vector3 scale=sphere.transform.lossyScale;
        float radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
        return FiniteVector(scale) && FiniteVector(sphere.transform.TransformPoint(sphere.center)) && Finite(radius) && radius>1e-7f;
    }
    private bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool FiniteVector(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
}
