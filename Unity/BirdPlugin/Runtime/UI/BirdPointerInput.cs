using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>SDK-independent logical Bird samples. Submit once per input update, before UI LateUpdate.</summary>
    [AddComponentMenu("Bird/UI/Pointer Input")]
    [DisallowMultipleComponent]
    public sealed class BirdPointerInput : MonoBehaviour
    {
        [SerializeField] string userId = "LocalUser";
        public string UserId { get { return userId; } }
        public Vector3 Origin { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 PreviousPosition { get; private set; }
        public bool HasMotionHistory { get; private set; }
        public bool IsPressed { get; private set; }
        public bool PressedThisSample { get; private set; }
        public uint Revision { get; private set; }
        bool tracked;
        public bool IsTracked { get { return isActiveAndEnabled && tracked; } }

        public void SetUser(string value)
        {
            if (userId == value) return;
            Cancel();
            userId = string.IsNullOrEmpty(value) ? "LocalUser" : value;
        }

        public void Submit(Vector3 origin, Vector3 position, bool isTracked, bool pressed)
        {
            if (!isActiveAndEnabled || !isTracked || !Finite(origin) || !Finite(position) ||
                !Finite((position-origin).sqrMagnitude)) { Cancel(); return; }
            HasMotionHistory = IsTracked;
            PreviousPosition = HasMotionHistory ? Position : position;
            PressedThisSample = HasMotionHistory && !IsPressed && pressed;
            Origin = origin;
            Position = position;
            IsPressed = pressed;
            tracked = true;
            Revision++;
        }

        public void Cancel()
        {
            tracked = false;
            IsPressed = PressedThisSample = HasMotionHistory = false;
            Revision++;
        }

        void OnDisable() { Cancel(); }
        static bool Finite(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
