using UdonSharp;
using UnityEngine;

// Optional local presentation. The logical element and its hit volume stay separate.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(200)]
public class BirdUiVisual : UdonSharpBehaviour
{
    public BirdUiElement source;
    [Tooltip("Separate presentation branch, without colliders, menu elements or this controller.")]
    public Transform visualRoot;
    public Renderer tintRenderer;
    public string colorProperty="_Color";
    [Min(0)] public float transitionSeconds=.14f;
    public bool automatic=true;
    [Header("Inactive")]
    public Vector3 inactivePosition,inactiveRotation,inactiveScale=Vector3.one;
    public Color inactiveColor=Color.white;
    public bool inactiveVisible;
    [Header("Enabled")]
    public Vector3 idlePosition,idleRotation,idleScale=Vector3.one;
    public Color idleColor=new Color(.13f,.2f,.27f);
    public bool idleVisible=true;
    [Header("Highlighted")]
    public Vector3 highlightedPosition,highlightedRotation,highlightedScale=Vector3.one;
    public Color highlightedColor=new Color(.1f,.85f,1);
    public bool highlightedVisible=true;
    [Header("Activated (pressed level, not action acknowledgement)")]
    public Vector3 activatedPosition,activatedRotation,activatedScale=Vector3.one;
    public Color activatedColor=new Color(1,.65f,.2f);
    public bool activatedVisible=true;
    [Header("Background")]
    public Vector3 backgroundPosition,backgroundRotation,backgroundScale=Vector3.one;
    public Color backgroundColor=new Color(.08f,.1f,.13f);
    public bool backgroundVisible=true;
    [HideInInspector] public float stepDelta=1f/72;
    [HideInInspector] public int displayedState=-1;
    [HideInInspector] public bool isTransitioning;
    [HideInInspector] public string configurationError;
    private Transform bound;
    private Renderer boundRenderer;
    private string boundProperty;
    private MaterialPropertyBlock original,working;
    private Vector3 restPosition,restScale,fromPosition,fromScale,toPosition,toScale,offset,angles,multiplier;
    private Quaternion restRotation,fromRotation,toRotation;
    private Color currentColor,fromColor,toColor,poseColor;
    private bool originalActive,targetVisible,poseVisible,dirty=true,stepping;
    private float elapsed;
    public void Initialize() { if(stepping) return; Restore(); dirty=true; }
    public void Refresh() { dirty=true; Process(); }
    private void OnDisable() { Restore(); }
    private void LateUpdate() { if(automatic) { stepDelta=Time.unscaledDeltaTime; Process(); } }
    private bool ConfigurationValid()
    {
        configurationError=null;
        if(source==null || visualRoot==null) configurationError="Assign source and separate visual root.";
        else if(transform.IsChildOf(visualRoot) || source.transform.IsChildOf(visualRoot)) configurationError="Keep controllers outside the visual branch.";
        else if(visualRoot.GetComponentInChildren<Collider>(true)!=null || visualRoot.GetComponentInChildren<BirdUiElement>(true)!=null) configurationError="Visual branch must not contain colliders or elements.";
        else if(tintRenderer!=null && (!tintRenderer.transform.IsChildOf(visualRoot) || string.IsNullOrEmpty(colorProperty))) configurationError="Tint renderer/property must belong to the visual branch.";
        else if(!Finite(transitionSeconds) || transitionSeconds<0) configurationError="Transition duration must be finite and nonnegative.";
        return configurationError==null;
    }
    public void Process()
    {
        if(stepping) return;
        if(!enabled || !gameObject.activeInHierarchy || !ConfigurationValid()) { Restore(); return; }
        if(!Finite(stepDelta) || stepDelta<0) return;
        stepping=true; Step(); stepping=false;
    }
    private void Step()
    {
        if(bound!=visualRoot || boundRenderer!=tintRenderer || boundProperty!=colorProperty)
        {
            Restore(); bound=visualRoot; boundRenderer=tintRenderer; boundProperty=colorProperty;
            restPosition=bound.localPosition; restRotation=bound.localRotation; restScale=bound.localScale; originalActive=bound.gameObject.activeSelf;
            if(!FiniteVector(restPosition) || !FiniteVector(restScale) || !FiniteRotation(restRotation)) { bound=null; boundRenderer=null; boundProperty=null; return; }
            if(boundRenderer!=null) { original=new MaterialPropertyBlock(); working=new MaterialPropertyBlock(); boundRenderer.GetPropertyBlock(original); boundRenderer.GetPropertyBlock(working); }
            displayedState=-1; dirty=true;
        }
        int next=source.enabled && source.gameObject.activeInHierarchy?source.state:0;
        if(!ReadPose(next)) { Restore(); return; }
        if(dirty || next!=displayedState)
        {
            bool first=displayedState<0; displayedState=next; dirty=false;
            fromPosition=bound.localPosition; fromRotation=bound.localRotation; fromScale=bound.localScale; fromColor=currentColor;
            toPosition=restPosition+offset; toRotation=restRotation*Quaternion.Euler(angles); toScale=Vector3.Scale(restScale,multiplier); toColor=poseColor; targetVisible=poseVisible;
            if(!FiniteVector(toPosition) || !FiniteVector(toScale) || !FiniteRotation(toRotation)) { Restore(); return; }
            elapsed=first?transitionSeconds:0;
            if(targetVisible) bound.gameObject.SetActive(true);
            if(bound==null || !enabled || !gameObject.activeInHierarchy) return;
        }
        elapsed=Mathf.Min(transitionSeconds,elapsed+stepDelta); isTransitioning=elapsed<transitionSeconds;
        float t=transitionSeconds==0?1:elapsed/transitionSeconds; t=t*t*(3-2*t);
        Vector3 position=Vector3.LerpUnclamped(fromPosition,toPosition,t),scale=Vector3.LerpUnclamped(fromScale,toScale,t);
        Quaternion rotation=Quaternion.SlerpUnclamped(fromRotation,toRotation,t); Color tint=Color.LerpUnclamped(fromColor,toColor,t);
        if(!FiniteVector(position) || !FiniteVector(scale) || !FiniteRotation(rotation) || !FiniteColor(tint)) { Restore(); return; }
        bound.localPosition=position; bound.localRotation=rotation; bound.localScale=scale; currentColor=tint;
        if(boundRenderer!=null) { working.SetColor(boundProperty,currentColor); boundRenderer.SetPropertyBlock(working); }
        if(!isTransitioning && !targetVisible) bound.gameObject.SetActive(false);
    }
    private bool ReadPose(int state)
    {
        if(state==0) { offset=inactivePosition; angles=inactiveRotation; multiplier=inactiveScale; poseColor=inactiveColor; poseVisible=inactiveVisible; }
        else if(state==1) { offset=idlePosition; angles=idleRotation; multiplier=idleScale; poseColor=idleColor; poseVisible=idleVisible; }
        else if(state==2) { offset=highlightedPosition; angles=highlightedRotation; multiplier=highlightedScale; poseColor=highlightedColor; poseVisible=highlightedVisible; }
        else if(state==3) { offset=activatedPosition; angles=activatedRotation; multiplier=activatedScale; poseColor=activatedColor; poseVisible=activatedVisible; }
        else if(state==4) { offset=backgroundPosition; angles=backgroundRotation; multiplier=backgroundScale; poseColor=backgroundColor; poseVisible=backgroundVisible; }
        else return false;
        return FiniteVector(offset) && FiniteVector(angles) && FiniteVector(multiplier) && multiplier.x>=0 && multiplier.y>=0 && multiplier.z>=0 && Finite(poseColor.r) && Finite(poseColor.g) && Finite(poseColor.b) && Finite(poseColor.a);
    }
    [RecursiveMethod]
    public void Restore()
    {
        Transform previous=bound; Renderer renderer=boundRenderer; MaterialPropertyBlock block=original;
        bound=null; boundRenderer=null; boundProperty=null; original=working=null; displayedState=-1; dirty=true; isTransitioning=false;
        if(renderer!=null) renderer.SetPropertyBlock(block);
        if(previous==null) return;
        previous.localPosition=restPosition; previous.localRotation=restRotation; previous.localScale=restScale; previous.gameObject.SetActive(originalActive);
    }
    private bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    private bool FiniteVector(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }
    private bool FiniteRotation(Quaternion v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z)&&Finite(v.w); }
    private bool FiniteColor(Color v) { return Finite(v.r)&&Finite(v.g)&&Finite(v.b)&&Finite(v.a); }
}
