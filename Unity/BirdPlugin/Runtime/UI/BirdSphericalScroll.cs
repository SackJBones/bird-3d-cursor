using System;
using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>Reach past a sphere's far surface to drive free rotation; withdraw to coast.</summary>
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
                    response < 0 || damping < 0 || maximumSpeed <= 0)
                { Cancel(); return; }
                if (hasMotionOwner && (!Eligible(motionOwner) || motionOwner.UserId != ownerId)) Cancel();
                if (!isActiveAndEnabled) return; // A cancellation listener may disable the component.

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
                    foreach (var pointer in pointers)
                    {
                        if (!Eligible(pointer) || !BirdSphereContact.TryGetBackSurface(sphere,pointer.Origin,pointer.Position,entryMargin,out normal,out hit)) continue;
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
                    if (!Eligible(active) || !BirdSphereContact.TryGetBackSurface(sphere,active.Origin,active.Position,0,out normal,out hit))
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

        static double Expm1(double x)
        {
            // Stable for very small steps without depending on newer .NET APIs.
            return Math.Abs(x) < 1e-5 ? x*(1+x*(.5+x/6)) : Math.Exp(x)-1;
        }
    }
}
