using System;
using UnityEngine;

namespace Bird3DCursor.UI
{
    [Serializable]
    public sealed class BirdMenuAppearance
    {
        [Tooltip("Offset in the visual parent's coordinates, relative to its bound rest pose.")]
        public Vector3 positionOffset;
        [Tooltip("Rotation after the rest rotation, in degrees.")]
        public Vector3 rotationOffset;
        public Vector3 scaleMultiplier=Vector3.one;
        public Color color=Color.white;
        public bool visible=true;
        public BirdMenuAppearance Copy() { return new BirdMenuAppearance { positionOffset=positionOffset,rotationOffset=rotationOffset,scaleMultiplier=scaleMultiplier,color=color,visible=visible }; }
    }

    /// <summary>Five optional visual poses. Never moves the element or its hit volume.</summary>
    [AddComponentMenu("Bird/UI/Menu Visual")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class BirdMenuVisual : MonoBehaviour
    {
        [SerializeField] BirdMenuElement source;
        [Tooltip("A separate presentation branch without colliders, menu elements, or this controller.")]
        [SerializeField] Transform visualRoot;
        [Tooltip("Optional renderer inside Visual Root. This component owns its property block while enabled.")]
        [SerializeField] Renderer tintRenderer;
        [SerializeField] string colorProperty="_Color";
        [Min(0)] [SerializeField] float transitionSeconds=.14f;
        [SerializeField] BirdMenuAppearance inactive=new BirdMenuAppearance { visible=false };
        [SerializeField] BirdMenuAppearance idle=new BirdMenuAppearance { color=new Color(.13f,.2f,.27f) };
        [SerializeField] BirdMenuAppearance highlighted=new BirdMenuAppearance { color=new Color(.1f,.85f,1) };
        [SerializeField] BirdMenuAppearance activated=new BirdMenuAppearance { color=new Color(1,.65f,.2f) };
        [SerializeField] BirdMenuAppearance background=new BirdMenuAppearance { color=new Color(.08f,.1f,.13f) };
        Transform bound;
        Renderer boundRenderer;
        string boundProperty;
        MaterialPropertyBlock original,working;
        Vector3 restPosition,restScale,fromPosition,fromScale,toPosition,toScale;
        Quaternion restRotation,fromRotation,toRotation;
        Color currentColor,fromColor,toColor;
        bool originalActive,targetVisible,dirty=true,stepping;
        float elapsed;
        BirdMenuVisualState state=(BirdMenuVisualState)(-1);
        public Transform VisualRoot { get { return visualRoot; } }
        public BirdMenuVisualState DisplayedState { get { return state; } }
        public bool IsTransitioning { get { return bound!=null && elapsed<transitionSeconds; } }
        public string ConfigurationError
        {
            get
            {
                if(source==null || visualRoot==null) return "Assign a source and a separate visual root.";
                if(transform.IsChildOf(visualRoot) || source.transform.IsChildOf(visualRoot)) return "Keep the visual controller and menu element outside the animated branch.";
                if(visualRoot.GetComponentInChildren<Collider>(true)!=null || visualRoot.GetComponentInChildren<BirdMenuElement>(true)!=null) return "The visual branch must not contain colliders or menu elements.";
                if(tintRenderer!=null && (!tintRenderer.transform.IsChildOf(visualRoot) || string.IsNullOrEmpty(colorProperty))) return "Assign a renderer inside the visual branch and its color property.";
                if(!Finite(transitionSeconds) || transitionSeconds<0) return "Transition duration must be finite and nonnegative.";
                return null;
            }
        }
        public void Configure(BirdMenuElement element,Transform visual,Renderer renderer=null,string property="_Color")
        {
            if(stepping) throw new InvalidOperationException("Configure visual feedback between updates.");
            Restore(); source=element; visualRoot=visual; tintRenderer=renderer; colorProperty=property; dirty=true;
        }
        public void SetAppearance(BirdMenuVisualState value,BirdMenuAppearance appearance)
        {
            if(!Valid(appearance)) throw new ArgumentException("Appearance needs finite values and nonnegative scale multipliers.");
            var copy=appearance.Copy();
            switch(value)
            {
                case BirdMenuVisualState.Inactive: inactive=copy; break;
                case BirdMenuVisualState.Enabled: idle=copy; break;
                case BirdMenuVisualState.Highlighted: highlighted=copy; break;
                case BirdMenuVisualState.Activated: activated=copy; break;
                case BirdMenuVisualState.Background: background=copy; break;
                default: throw new ArgumentOutOfRangeException("value");
            }
            dirty=true;
        }
        public BirdMenuAppearance GetAppearance(BirdMenuVisualState value) { var pose=Pose(value); return pose==null?null:pose.Copy(); }
        public void SetTransition(float seconds)
        { if(!Finite(seconds) || seconds<0) throw new ArgumentOutOfRangeException("seconds"); transitionSeconds=seconds; dirty=true; }
        void OnValidate() { dirty=true; }
        void OnDisable() { Restore(); }
        void LateUpdate() { Apply(Time.unscaledDeltaTime); }
        public void Apply(float dt)
        {
            if(stepping) return;
            if(!isActiveAndEnabled || ConfigurationError!=null) { Restore(); return; }
            if(!Finite(dt) || dt<0) return;
            stepping=true;
            try
            {
                if(bound!=visualRoot || boundRenderer!=tintRenderer || boundProperty!=colorProperty)
                {
                    Restore(); bound=visualRoot; boundRenderer=tintRenderer; boundProperty=colorProperty;
                    restPosition=bound.localPosition; restRotation=bound.localRotation; restScale=bound.localScale; originalActive=bound.gameObject.activeSelf;
                    if(!Finite(restPosition) || !Finite(restScale) || !Finite(restRotation)) { bound=null; boundRenderer=null; boundProperty=null; return; }
                    if(boundRenderer!=null)
                    {
                        original=new MaterialPropertyBlock(); working=new MaterialPropertyBlock();
                        boundRenderer.GetPropertyBlock(original); boundRenderer.GetPropertyBlock(working);
                    }
                    state=(BirdMenuVisualState)(-1); dirty=true;
                }
                var next=source.isActiveAndEnabled?source.State:BirdMenuVisualState.Inactive;
                var pose=Pose(next); if(!Valid(pose)) { Restore(); return; }
                if(dirty || next!=state)
                {
                    bool first=(int)state<0; state=next; dirty=false;
                    fromPosition=bound.localPosition; fromRotation=bound.localRotation; fromScale=bound.localScale; fromColor=currentColor;
                    toPosition=restPosition+pose.positionOffset; toRotation=restRotation*Quaternion.Euler(pose.rotationOffset); toScale=Vector3.Scale(restScale,pose.scaleMultiplier); toColor=pose.color; targetVisible=pose.visible;
                    if(!Finite(toPosition) || !Finite(toScale) || !Finite(toRotation)) { Restore(); return; }
                    elapsed=first?transitionSeconds:0;
                    if(targetVisible) bound.gameObject.SetActive(true);
                    if(bound==null || !isActiveAndEnabled) return;
                }
                elapsed=Mathf.Min(transitionSeconds,elapsed+dt);
                float t=transitionSeconds==0?1:elapsed/transitionSeconds; t=t*t*(3-2*t);
                Vector3 position=Vector3.LerpUnclamped(fromPosition,toPosition,t),scale=Vector3.LerpUnclamped(fromScale,toScale,t);
                Quaternion rotation=Quaternion.SlerpUnclamped(fromRotation,toRotation,t); Color tint=Color.LerpUnclamped(fromColor,toColor,t);
                if(!Finite(position) || !Finite(scale) || !Finite(rotation) || !Finite(tint)) { Restore(); return; }
                bound.localPosition=position; bound.localRotation=rotation; bound.localScale=scale; currentColor=tint;
                if(boundRenderer!=null) { working.SetColor(boundProperty,currentColor); boundRenderer.SetPropertyBlock(working); }
                if(elapsed>=transitionSeconds && !targetVisible) bound.gameObject.SetActive(false);
            }
            finally { stepping=false; }
        }
        public void Restore()
        {
            // Clear ownership before SetActive: presentation callbacks can disable us.
            var previous=bound; var renderer=boundRenderer; var block=original;
            bound=null; boundRenderer=null; boundProperty=null; original=working=null; state=(BirdMenuVisualState)(-1); dirty=true;
            if(renderer!=null) renderer.SetPropertyBlock(block);
            if(previous==null) return;
            previous.localPosition=restPosition; previous.localRotation=restRotation; previous.localScale=restScale; previous.gameObject.SetActive(originalActive);
        }
        BirdMenuAppearance Pose(BirdMenuVisualState value)
        { switch(value) { case BirdMenuVisualState.Inactive:return inactive; case BirdMenuVisualState.Enabled:return idle; case BirdMenuVisualState.Highlighted:return highlighted; case BirdMenuVisualState.Activated:return activated; case BirdMenuVisualState.Background:return background; default:return null; } }
        static bool Valid(BirdMenuAppearance p) { return p!=null && Finite(p.positionOffset) && Finite(p.rotationOffset) && Finite(p.scaleMultiplier) && p.scaleMultiplier.x>=0 && p.scaleMultiplier.y>=0 && p.scaleMultiplier.z>=0 && Finite(p.color.r) && Finite(p.color.g) && Finite(p.color.b) && Finite(p.color.a); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        static bool Finite(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }
        static bool Finite(Quaternion v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z)&&Finite(v.w); }
        static bool Finite(Color v) { return Finite(v.r)&&Finite(v.g)&&Finite(v.b)&&Finite(v.a); }
    }
}
