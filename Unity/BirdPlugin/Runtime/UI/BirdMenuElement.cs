using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bird3DCursor.UI
{
    public enum BirdMenuVisualState { Inactive, Enabled, Highlighted, Activated, Background }
    [Serializable] public sealed class BirdPointerEvent : UnityEvent<BirdPointerInput> { }
    [Serializable] public sealed class BirdMenuStateEvent : UnityEvent<BirdMenuVisualState> { }

    /// <summary>A collider-backed action. Its hit volume is independent of any cursor/feedback size.</summary>
    [AddComponentMenu("Bird/UI/Menu Element")]
    [DisallowMultipleComponent]
    public sealed class BirdMenuElement : MonoBehaviour
    {
        public enum Activation { SelectAtPoint, SelectThrough, EnterThrough, DirectionalPass }
        [Tooltip("Box, sphere, capsule or convex mesh hit volume. Keep it separate from animated visuals.")]
        [SerializeField] Collider target;
        [SerializeField] BirdMenuPanel panel;
        [SerializeField] Activation activation = Activation.SelectThrough;
        [Tooltip("Allow this control (for example Back) while its panel has an open child.")]
        [SerializeField] bool activeInBackground;
        [SerializeField] Vector3 localPassDirection = Vector3.up;
        [Range(0,180)] [SerializeField] float passAngle = 45;
        [Tooltip("Higher priority wins when one gesture crosses several controls. Ties use nearest hit.")]
        [SerializeField] int priority;
        [SerializeField] BirdPointerEvent activated = new BirdPointerEvent();
        [SerializeField] BirdMenuStateEvent stateChanged = new BirdMenuStateEvent();
        readonly Dictionary<BirdPointerInput, bool> wasThrough = new Dictionary<BirdPointerInput, bool>();
        bool hovered, pressed;
        public BirdMenuPanel Panel { get { return panel; } }
        public int Priority { get { return priority; } }
        public BirdPointerEvent Activated { get { return activated; } }
        public BirdMenuStateEvent StateChanged { get { return stateChanged; } }
        public BirdMenuVisualState State { get; private set; }

        void Reset() { target = GetComponent<Collider>(); panel = GetComponentInParent<BirdMenuPanel>(); }
        void OnDisable() { wasThrough.Clear(); hovered = pressed = false; SetState(BirdMenuVisualState.Inactive); }

        public void Configure(Collider collider, BirdMenuPanel owner, Activation mode,
            bool allowBackground = false, Vector3? passDirection = null, int actionPriority = 0)
        {
            target = collider;
            panel = owner;
            activation = mode;
            activeInBackground = allowBackground;
            localPassDirection = passDirection ?? Vector3.up;
            priority = actionPriority;
            wasThrough.Clear();
        }

        internal bool Accepts(BirdPointerInput pointer)
        {
            return AvailableTarget() &&
                pointer != null && pointer.IsTracked && (panel == null ||
                (panel.Accepts(pointer) && (panel.State != BirdMenuPanel.PanelState.Background || activeInBackground)));
        }

        internal void BeginFrame() { hovered = pressed = false; }
        internal void ForgetPointers() { wasThrough.Clear(); }

        internal bool Evaluate(BirdPointerInput pointer, bool newSample, out float distance)
        {
            distance = float.PositiveInfinity;
            if (!Accepts(pointer)) { if (pointer != null) wasThrough.Remove(pointer); return false; }
            Vector3 delta = pointer.Position-pointer.Origin;
            float range = delta.magnitude;
            RaycastHit hit;
            bool contains = Contains(pointer.Position);
            bool through = contains || Contains(pointer.Origin);
            if (range > .000001f && target.Raycast(new Ray(pointer.Origin,delta/range), out hit, range))
            { through = true; distance = hit.distance; }
            else if (through) distance = 0;
            hovered |= through;
            pressed |= through && pointer.IsPressed;
            bool prior;
            wasThrough.TryGetValue(pointer, out prior);
            wasThrough[pointer] = through;
            if (!newSample) return false;
            switch (activation)
            {
                case Activation.SelectAtPoint: return contains && pointer.PressedThisSample;
                case Activation.SelectThrough: return through && pointer.PressedThisSample;
                case Activation.EnterThrough: return through && !prior && pointer.HasMotionHistory;
                case Activation.DirectionalPass:
                    if (!pointer.HasMotionHistory) return false;
                    Vector3 motion = pointer.Position-pointer.PreviousPosition;
                    float length = motion.magnitude;
                    if (length < .000001f) return false;
                    Vector3 direction = transform.TransformDirection(localPassDirection).normalized;
                    if (direction.sqrMagnitude < .5f || Vector3.Dot(motion/length,direction) < Mathf.Cos(passAngle*Mathf.Deg2Rad)) return false;
                    if (!target.Raycast(new Ray(pointer.PreviousPosition,motion/length), out hit, length)) return false;
                    distance = hit.distance;
                    return true;
                default: return false;
            }
        }

        bool Contains(Vector3 point) { return (target.ClosestPoint(point)-point).sqrMagnitude < 1e-10f; }

        bool AvailableTarget()
        {
            if (!isActiveAndEnabled || target == null || !target.enabled || !target.gameObject.activeInHierarchy) return false;
            var mesh = target as MeshCollider;
            return target is BoxCollider || target is SphereCollider || target is CapsuleCollider ||
                (mesh != null && mesh.convex && mesh.sharedMesh != null);
        }

        internal void Invoke(BirdPointerInput pointer)
        {
            if (Accepts(pointer)) activated.Invoke(pointer);
        }

        internal void FinishFrame()
        {
            bool available = AvailableTarget();
            if (!available || (panel != null && (!panel.isActiveAndEnabled || panel.State == BirdMenuPanel.PanelState.Closed)))
                SetState(BirdMenuVisualState.Inactive);
            else if (panel != null && panel.State == BirdMenuPanel.PanelState.Background && !activeInBackground)
                SetState(BirdMenuVisualState.Background);
            else SetState(pressed ? BirdMenuVisualState.Activated : hovered ? BirdMenuVisualState.Highlighted : BirdMenuVisualState.Enabled);
        }

        void SetState(BirdMenuVisualState state)
        {
            if (State == state) return;
            State = state;
            stateChanged.Invoke(state);
        }
    }
}
