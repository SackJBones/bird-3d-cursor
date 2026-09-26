using UnityEngine;
using UnityEngine.Events;

namespace Bird3DCursor.UI
{
    /// <summary>A menu branch with per-user focus, shared by that user's pointers.</summary>
    [AddComponentMenu("Bird/UI/Menu Panel")]
    [DisallowMultipleComponent]
    public sealed class BirdMenuPanel : MonoBehaviour
    {
        public enum PanelState { Closed, Open, Background }
        [Tooltip("A separate child containing the menu visuals and controls. Keep this component outside that child.")]
        [SerializeField] GameObject content;
        [SerializeField] BirdMenuPanel parent;
        [SerializeField] bool closeWhenFocusLost = true;
        [SerializeField] UnityEvent opened = new UnityEvent();
        [SerializeField] UnityEvent closed = new UnityEvent();
        public PanelState State { get; private set; }
        public string Owner { get; private set; }
        public BirdMenuPanel Parent { get { return parent; } }
        public bool CloseWhenFocusLost { get { return closeWhenFocusLost; } }
        public UnityEvent Opened { get { return opened; } }
        public UnityEvent Closed { get { return closed; } }
        BirdMenuPanel child;
        bool closing, changingChild;

        void Awake() { ShowContent(false); }
        void OnDisable() { Close(); }

        public void Configure(GameObject contentRoot, BirdMenuPanel parentPanel = null)
        {
            Close();
            content = contentRoot;
            parent = parentPanel;
            ShowContent(false);
        }

        public bool Accepts(BirdPointerInput pointer)
        {
            return pointer != null && pointer.IsTracked && isActiveAndEnabled &&
                State != PanelState.Closed && Owner == pointer.UserId;
        }

        public void Open(BirdPointerInput pointer)
        {
            if (closing || !isActiveAndEnabled || pointer == null || !pointer.IsTracked || !ValidHierarchy()) return;
            if (State != PanelState.Closed) return; // An open branch cannot be stolen by another user.
            if (parent != null)
            {
                if (parent.changingChild || !parent.Accepts(pointer)) return;
                var owningParent = parent;
                owningParent.changingChild = true;
                try
                {
                    if (owningParent.child != null && owningParent.child != this) owningParent.child.Close();
                }
                finally { if (owningParent != null) owningParent.changingChild = false; }
                // Closing the old child may disable us, close the root, or reconfigure this branch.
                if (!isActiveAndEnabled || parent == null || parent != owningParent || !parent.Accepts(pointer)) return;
                parent.child = this;
                parent.State = PanelState.Background;
            }
            Owner = pointer.UserId;
            State = PanelState.Open;
            ShowContent(true);
            opened.Invoke();
        }

        public void Close()
        {
            if (closing || State == PanelState.Closed) return;
            closing = true;
            try
            {
                State = PanelState.Closed;
                Owner = null;
                if (child != null) child.Close();
                child = null;
                ShowContent(false);
                if (parent != null && parent.child == this)
                {
                    parent.child = null;
                    if (parent.State == PanelState.Background) parent.State = PanelState.Open;
                }
                closed.Invoke();
            }
            finally { closing = false; }
        }

        public void CloseAll()
        {
            var root = Root;
            if (root != null) root.Close(); else Close();
        }

        internal BirdMenuPanel Root
        {
            get
            {
                if (!ValidHierarchy()) return null;
                var root = this;
                while (root.parent != null) root = root.parent;
                return root;
            }
        }

        bool ValidHierarchy()
        {
            // Detect Inspector-created cycles without allocating a collection.
            BirdMenuPanel slow = this, fast = this;
            do
            {
                slow = slow != null ? slow.parent : null;
                fast = fast != null && fast.parent != null ? fast.parent.parent : null;
                if (slow != null && slow == fast) return false;
            } while (fast != null);
            return true;
        }

        void ShowContent(bool visible)
        {
            // A malformed content assignment must never disable its controller or an ancestor.
            if (content != null && content != gameObject && !transform.IsChildOf(content.transform))
                content.SetActive(visible);
        }
    }
}
