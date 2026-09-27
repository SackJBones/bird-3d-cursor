using System;
using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>Enter a sphere, extend through its back to rotate, then withdraw to coast.</summary>
    [AddComponentMenu("Bird/UI/Spherical Scroll")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class BirdSphericalScroll : MonoBehaviour
    {
        [SerializeField] SphereCollider sphere;
        [SerializeField] Transform rotationTarget;
        [SerializeField] BirdPointerInput[] pointers = new BirdPointerInput[0];
        [SerializeField] BirdMenuPanel panel;
        [Tooltip("Meters past the back surface required to begin. Driving stops at the back surface.")]
        [Min(0)] [SerializeField] float entryMargin = .003f;
        [Tooltip("Input-following rate in inverse seconds; recovered scene approximately 10.")]
        [Min(0)] [SerializeField] float response = 10;
        [Tooltip("Angular velocity decay in inverse seconds; recovered scene approximately 0.99.")]
        [Min(0)] [SerializeField] float damping = .99f;
        [Min(1)] [SerializeField] float maximumSpeed = 720;
        [SerializeField] BirdPointerEvent started = new BirdPointerEvent();
        [SerializeField] BirdPointerEvent stopped = new BirdPointerEvent();

        BirdPointerInput active, motionOwner;
        string ownerId;
        Vector3 previousNormal, angularVelocity;
        uint revision;
        bool stepping, hasMotionOwner;
        BirdPointerInput[] observedPointers=new BirdPointerInput[0];
        bool[] seen=new bool[0], armed=new bool[0], entered=new bool[0], continuous=new bool[0];
        uint[] seenRevision=new uint[0];
        string[] seenUser=new string[0];
        double[] lastRange=new double[0];
        float[] sampleAge=new float[0];
        Vector3 previousCenter;
        float previousRadius;
        bool hasSphereHistory;
        public BirdPointerInput ActivePointer { get { return active; } }
        public Vector3 AngularVelocity { get { return angularVelocity; } } // World radians / second.
        public BirdPointerEvent Started { get { return started; } }
        public BirdPointerEvent Stopped { get { return stopped; } }

        public void Configure(SphereCollider volume, Transform target, BirdPointerInput[] inputs, BirdMenuPanel owner = null)
        {
            if (stepping) throw new InvalidOperationException("Configure spherical scrolling between input updates.");
            Cancel(); sphere=volume; rotationTarget=target; panel=owner;
            pointers=inputs != null ? (BirdPointerInput[])inputs.Clone() : new BirdPointerInput[0];
        }

        void LateUpdate() { Process(Time.unscaledDeltaTime); }
        void OnDisable() { Cancel(); }

        public void Cancel()
        {
            var previous=active;
            active=motionOwner=null; ownerId=null; hasMotionOwner=false; angularVelocity=Vector3.zero;
            hasSphereHistory=false;
            for(int i=0;i<seen.Length;i++) seen[i]=armed[i]=entered[i]=continuous[i]=false;
            if (previous != null) stopped.Invoke(previous);
        }

        bool Eligible(BirdPointerInput pointer)
        {
            return pointer != null && pointer.IsTracked && (panel == null ||
                (panel.Accepts(pointer) && panel.State == BirdMenuPanel.PanelState.Open));
        }

        public void Process(float dt)
        {
            if (stepping || !isActiveAndEnabled) return;
            stepping=true;
            try
            {
                Vector3 center;
                float radius;
                if (!BirdSphereContact.Finite(dt) || dt <= 0 || dt > .25f || !BirdSphereContact.TryGetSphere(sphere,out center,out radius) || rotationTarget == null ||
                    !rotationTarget.gameObject.activeInHierarchy || !BirdSphereContact.Finite(response) ||
                    !BirdSphereContact.Finite(damping) || !BirdSphereContact.Finite(maximumSpeed) ||
                    response < 0 || damping < 0 || maximumSpeed <= 0 || !BirdSphereContact.Finite(entryMargin) || entryMargin<0)
                { Cancel(); return; }
                PrepareHistory();
                if (hasMotionOwner && (!Eligible(motionOwner) || motionOwner.UserId != ownerId)) Cancel();
                if (!isActiveAndEnabled) return; // A cancellation listener may disable the component.
                if(hasSphereHistory && ((center-previousCenter).sqrMagnitude>Mathf.Max(1e-10f,radius*radius*1e-10f) ||
                    Mathf.Abs(radius-previousRadius)>Mathf.Max(1e-6f,radius*1e-5f))) Cancel();
                if(!isActiveAndEnabled) return;
                previousCenter=center; previousRadius=radius; hasSphereHistory=true;
                ObserveEntries(center,radius,dt);
                if(hasMotionOwner && !HasContinuousSample(motionOwner)) Cancel();
                if(!isActiveAndEnabled) return;

                Vector3 normal, hit;
                bool driving=Eligible(active) && BirdSphereContact.TryGetBackSurface(sphere,active.Origin,active.Position,0,out normal,out hit);
                if (!driving && active != null)
                {
                    var previous=active; active=null; stopped.Invoke(previous);
                    if (!isActiveAndEnabled) return;
                }
                bool acquired=false;
                if (active == null)
                {
                    for(int i=0;i<observedPointers.Length;i++)
                    {
                        var pointer=observedPointers[i];
                        if (!entered[i] || !Eligible(pointer) || pointer.UserId!=seenUser[i] || pointer.Revision!=seenRevision[i] ||
                            !BirdSphereContact.TryGetBackSurface(sphere,pointer.Origin,pointer.Position,entryMargin,out normal,out hit)) continue;
                        // Never derive velocity between different hands' contact points.
                        if (motionOwner != null && motionOwner != pointer) angularVelocity=Vector3.zero;
                        active=motionOwner=pointer; ownerId=pointer.UserId; hasMotionOwner=true;
                        previousNormal=normal; revision=pointer.Revision; acquired=true;
                        started.Invoke(pointer);
                        break;
                    }
                }
                if (!isActiveAndEnabled || rotationTarget == null) return;

                Vector3 inputVelocity=Vector3.zero;
                if (active != null)
                {
                    if (!Eligible(active) || active.UserId!=ownerId || (acquired && revision!=active.Revision) || !BirdSphereContact.TryGetBackSurface(sphere,active.Origin,active.Position,0,out normal,out hit))
                    { Cancel(); return; }
                    if (!acquired && revision != active.Revision)
                    {
                        Vector3 cross=Vector3.Cross(previousNormal,normal);
                        float sine=cross.magnitude, cosine=Mathf.Clamp(Vector3.Dot(previousNormal,normal),-1,1);
                        // Exact opposite contacts have no unique axis: rebase, never invent a spin.
                        if (!active.HasMotionHistory) angularVelocity=Vector3.zero;
                        else if (sine > 1e-6f)
                            inputVelocity=cross/sine*Mathf.Min(Mathf.Atan2(sine,cosine)/dt,maximumSpeed*Mathf.Deg2Rad);
                        previousNormal=normal; revision=active.Revision;
                    }
                }

                float rate=damping+(active != null ? response : 0);
                Vector3 equilibrium=rate > 0 && active != null ? inputVelocity*(response/rate) : Vector3.zero;
                float decay=Mathf.Exp(-rate*dt);
                float integral=rate > .0001f ? (float)(-Expm1(-rate*dt)/rate) : dt;
                Vector3 rotation=equilibrium*dt+(angularVelocity-equilibrium)*integral;
                angularVelocity=equilibrium+(angularVelocity-equilibrium)*decay;
                float angle=rotation.magnitude;
                if (angle > 1e-8f)
                {
                    rotationTarget.RotateAround(sphere.transform.TransformPoint(sphere.center),rotation/angle,angle*Mathf.Rad2Deg);
                    // Child menu colliders must agree with their rendered positions on the next query.
                    Physics.SyncTransforms();
                }
            }
            finally { stepping=false; }
        }

        void PrepareHistory()
        {
            int count=pointers==null?0:pointers.Length;
            bool changed=observedPointers.Length!=count;
            if(!changed) for(int i=0;i<count;i++) if(observedPointers[i]!=pointers[i]) changed=true;
            if(!changed) return;
            Cancel(); observedPointers=new BirdPointerInput[count];
            seen=new bool[count]; armed=new bool[count]; entered=new bool[count]; continuous=new bool[count];
            seenRevision=new uint[count]; seenUser=new string[count]; lastRange=new double[count]; sampleAge=new float[count];
            for(int i=0;i<count;i++) observedPointers[i]=pointers[i];
        }

        bool HasContinuousSample(BirdPointerInput pointer)
        {
            for(int i=0;i<observedPointers.Length;i++) if(observedPointers[i]==pointer) return continuous[i];
            return false;
        }

        void ObserveEntries(Vector3 center,float radius,float dt)
        {
            double inset=Math.Min(entryMargin,radius*.1f), insideRadius=radius-inset;
            double epsilon=Math.Max(1e-6,radius*1e-6);
            for(int i=0;i<observedPointers.Length;i++)
            {
                var pointer=observedPointers[i]; entered[i]=false;
                if(!Eligible(pointer)) { seen[i]=armed[i]=continuous[i]=false; continue; }
                if(seen[i] && pointer.Revision==seenRevision[i] && pointer.UserId==seenUser[i])
                {
                    sampleAge[i]+=dt; continuous[i]=sampleAge[i]<=.25f;
                    if(!continuous[i]) armed[i]=false;
                    continue;
                }
                continuous[i]=seen[i] && pointer.HasMotionHistory && pointer.UserId==seenUser[i] &&
                    pointer.Revision==unchecked(seenRevision[i]+1u) && sampleAge[i]<=.25f;
                if(!continuous[i]) armed[i]=false;
                Vector3 delta=pointer.Position-pointer.Origin, offset=pointer.Position-center;
                double range=Math.Sqrt((double)delta.x*delta.x+(double)delta.y*delta.y+(double)delta.z*delta.z);
                double distance=Math.Sqrt((double)offset.x*offset.x+(double)offset.y*offset.y+(double)offset.z*offset.z);
                if(distance<insideRadius) armed[i]=true;
                else if(armed[i] && distance>radius)
                {
                    Vector3 normal,hit;
                    // A translating hand/volume or an angular sweep at fixed reach
                    // cannot manufacture outward contact. Keep only the back margin band.
                    double extension=range-lastRange[i];
                    if(extension>=-epsilon && BirdSphereContact.TryGetBackSurface(sphere,pointer.Origin,pointer.Position,0,out normal,out hit))
                    {
                        bool beyondMargin=BirdSphereContact.TryGetBackSurface(sphere,pointer.Origin,pointer.Position,entryMargin,out normal,out hit);
                        entered[i]=extension>epsilon && beyondMargin;
                        if(beyondMargin) armed[i]=false; // A non-outward exit also consumes arming.
                    }
                    else armed[i]=false;
                }
                seen[i]=true; seenRevision[i]=pointer.Revision; seenUser[i]=pointer.UserId; lastRange[i]=range; sampleAge[i]=0;
            }
        }

        static double Expm1(double x)
        {
            // Stable for very small steps without depending on newer .NET APIs.
            return Math.Abs(x) < 1e-5 ? x*(1+x*(.5+x/6)) : Math.Exp(x)-1;
        }
    }
}
