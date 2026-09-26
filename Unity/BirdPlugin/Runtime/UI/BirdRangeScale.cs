using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bird3DCursor.UI
{
    [Serializable] public sealed class BirdScaleEvent : UnityEvent<float> { }

    /// <summary>Explicitly armed, point-through range scaling around a target's own pivot.</summary>
    [AddComponentMenu("Bird/UI/Range Scale")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class BirdRangeScale : MonoBehaviour
    {
        [Tooltip("A fixed box, sphere, capsule or convex mesh, outside the scaled target hierarchy.")]
        [SerializeField] Collider engagementVolume;
        [SerializeField] Transform scaleTarget;
        [SerializeField] BirdPointerInput[] pointers = new BirdPointerInput[0];
        [SerializeField] BirdMenuPanel panel;
        [Tooltip("Multipliers relative to the configured rest scale. The interval must contain 1.")]
        [Min(.0001f)] [SerializeField] float minimumFactor = .25f;
        [Min(1)] [SerializeField] float maximumFactor = 4;
        [Tooltip("2 preserves the recovered map's squared range-ratio law.")]
        [Min(.01f)] [SerializeField] float rangeExponent = 2;
        [Tooltip("Log-scale response in inverse seconds. Zero follows immediately.")]
        [Min(0)] [SerializeField] float response = 12;
        [SerializeField] BirdPointerEvent started = new BirdPointerEvent();
        [SerializeField] BirdPointerEvent stopped = new BirdPointerEvent();
        [SerializeField] BirdScaleEvent changed = new BirdScaleEvent();

        Transform bound, parent;
        BirdPointerInput active;
        string owner;
        Vector3 restScale, expectedScale;
        Matrix4x4 volumeFrame, parentFrame;
        double currentLog, desiredLog, previousDesired, anchorLogRange, anchorLogScale;
        bool stepping, armed;
        uint revision;
        public bool ScalingEnabled { get { return armed; } }
        public BirdPointerInput ActivePointer { get { return active; } }
        public float Factor { get { return (float)Math.Exp(currentLog); } }
        public float DesiredFactor { get { return (float)Math.Exp(desiredLog); } }
        public BirdPointerEvent Started { get { return started; } }
        public BirdPointerEvent Stopped { get { return stopped; } }
        public BirdScaleEvent Changed { get { return changed; } }

        public void Configure(Collider volume, Transform target, BirdPointerInput[] inputs, BirdMenuPanel ownerPanel = null)
        {
            if (stepping) throw new InvalidOperationException("Configure range scaling between input updates.");
            Cancel(); engagementVolume=volume; scaleTarget=target; panel=ownerPanel;
            pointers=inputs != null ? (BirdPointerInput[])inputs.Clone() : new BirdPointerInput[0];
            bound=null; Bind();
        }

        public void SetLimits(float minimum, float maximum, float exponent = 2, float responseRate = 12)
        {
            if (stepping) throw new InvalidOperationException("Set scaling limits between input updates.");
            if (!Finite(minimum) || minimum<=0 || minimum>1 || !Finite(maximum) || maximum<1 ||
                !Finite(exponent) || exponent<=0 || !Finite(responseRate) || responseRate<0)
                throw new ArgumentOutOfRangeException("Scaling limits must be finite, positive and contain the rest factor 1.");
            Cancel(); minimumFactor=minimum; maximumFactor=maximum; rangeExponent=exponent; response=responseRate;
            if (Bind()) Apply(Math.Max(Math.Log(minimum),Math.Min(Math.Log(maximum),currentLog)));
        }

        public void StartScaling() { if (isActiveAndEnabled && ValidSettings() && Bind()) armed=true; }
        public void StopScaling() { Cancel(); }
        public void Cancel() { armed=false; EndGesture(); }
        public void ResetScale() { Cancel(); if (ValidSettings() && Bind()) Apply(0); }
        void OnDisable() { Cancel(); }
        void LateUpdate() { Process(Time.unscaledDeltaTime); }

        bool Bind()
        {
            if (scaleTarget==null || !scaleTarget.gameObject.activeInHierarchy) return false;
            if (bound==scaleTarget) return true;
            EndGesture(); bound=scaleTarget; restScale=expectedScale=bound.localScale;
            currentLog=desiredLog=previousDesired=0;
            return ValidScale(restScale);
        }

        bool ValidSettings()
        {
            return Finite(minimumFactor) && minimumFactor>0 && minimumFactor<=1 &&
                Finite(maximumFactor) && maximumFactor>=1 && Finite(rangeExponent) && rangeExponent>0 &&
                Finite(response) && response>=0;
        }

        bool Eligible(BirdPointerInput pointer)
        {
            return pointer!=null && pointer.IsTracked && (panel==null ||
                panel.Accepts(pointer) && panel.State==BirdMenuPanel.PanelState.Open);
        }

        bool Contact(BirdPointerInput pointer, out double range)
        {
            range=0;
            if (!Eligible(pointer) || engagementVolume==null || !engagementVolume.enabled || !engagementVolume.gameObject.activeInHierarchy ||
                engagementVolume.transform.IsChildOf(scaleTarget)) return false;
            var mesh=engagementVolume as MeshCollider;
            if (!(engagementVolume is BoxCollider || engagementVolume is SphereCollider || engagementVolume is CapsuleCollider ||
                mesh!=null && mesh.convex && mesh.sharedMesh!=null)) return false;
            Vector3 delta=pointer.Position-pointer.Origin;
            range=Math.Sqrt((double)delta.x*delta.x+(double)delta.y*delta.y+(double)delta.z*delta.z);
            if (range<=1e-5 || double.IsNaN(range) || double.IsInfinity(range)) return false;
            if ((engagementVolume.ClosestPoint(pointer.Origin)-pointer.Origin).sqrMagnitude<1e-10f) return true;
            RaycastHit hit;
            return engagementVolume.Raycast(new Ray(pointer.Origin,delta/(float)range),out hit,(float)range);
        }

        public void Process(float dt)
        {
            if (stepping || !isActiveAndEnabled || !armed) return;
            stepping=true;
            try
            {
                if (!Finite(dt) || dt<=0 || dt>.25f || !ValidSettings() || !Bind() || !ValidScale(restScale) ||
                    !ValidScale(scaleTarget.localScale) || !Approximately(scaleTarget.localScale,expectedScale))
                { Cancel(); return; }
                double range;
                if (active!=null && (!Contact(active,out range) || active.UserId!=owner || !active.HasMotionHistory && active.Revision!=revision ||
                    scaleTarget.parent!=parent || ParentFrame()!=parentFrame || engagementVolume.transform.localToWorldMatrix!=volumeFrame))
                { EndGesture(); return; }
                if (active==null)
                {
                    foreach (var pointer in pointers)
                    {
                        if (!Contact(pointer,out range)) continue;
                        active=pointer; owner=pointer.UserId; anchorLogRange=Math.Log(range); anchorLogScale=currentLog;
                        revision=pointer.Revision;
                        parent=scaleTarget.parent; parentFrame=ParentFrame(); volumeFrame=engagementVolume.transform.localToWorldMatrix;
                        previousDesired=desiredLog=currentLog;
                        started.Invoke(pointer);
                        return; // Rebase only; callbacks may cancel, disable or destroy the target.
                    }
                    return;
                }
                if (!Contact(active,out range)) { EndGesture(); return; }
                revision=active.Revision;
                desiredLog=Math.Max(Math.Log(minimumFactor),Math.Min(Math.Log(maximumFactor),anchorLogScale+rangeExponent*(Math.Log(range)-anchorLogRange)));
                double next=desiredLog;
                if (response>0)
                {
                    // Exact first-order response to a linearly changing log target.
                    // Series terms avoid cancellation when the elapsed interval is tiny.
                    double interval=response*dt;
                    double decay=Math.Exp(-interval);
                    double weight=interval<.001 ? interval*(1-interval*.5+interval*interval/6) : 1-decay;
                    double ramp=interval<.001 ? interval*(.5-interval/6+interval*interval/24) : 1-weight/interval;
                    next=currentLog+(previousDesired-currentLog)*weight+(desiredLog-previousDesired)*ramp;
                }
                previousDesired=desiredLog;
                Apply(next);
            }
            finally { stepping=false; }
        }

        Matrix4x4 ParentFrame() { return scaleTarget.parent==null ? Matrix4x4.identity : scaleTarget.parent.localToWorldMatrix; }
        void EndGesture()
        {
            var previous=active; active=null; owner=null;
            desiredLog=previousDesired=currentLog;
            if (previous!=null) stopped.Invoke(previous);
        }
        void Apply(double logarithm)
        {
            if (bound==null) return;
            logarithm=Math.Max(Math.Log(minimumFactor),Math.Min(Math.Log(maximumFactor),logarithm));
            float factor=(float)Math.Exp(logarithm);
            Vector3 size=restScale*factor;
            if (!ValidScale(size)) { Cancel(); return; }
            double prior=currentLog; currentLog=logarithm; expectedScale=size; bound.localScale=size;
            Physics.SyncTransforms();
            if (Math.Abs(prior-currentLog)>1e-9) changed.Invoke(factor);
        }
        static bool Approximately(Vector3 a,Vector3 b) { return (a-b).sqrMagnitude<=1e-10f*b.sqrMagnitude; }
        static bool ValidScale(Vector3 v) { return Finite(v.sqrMagnitude) && Mathf.Abs(v.x)>1e-7f && Mathf.Abs(v.y)>1e-7f && Mathf.Abs(v.z)>1e-7f; }
        static bool Finite(float v) { return !float.IsNaN(v)&&!float.IsInfinity(v); }
    }
}
