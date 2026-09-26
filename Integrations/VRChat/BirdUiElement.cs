using UdonSharp;
using UnityEngine;
using VRC.Udon;

public enum BirdUiActivation { SelectAtPoint, SelectThrough, EnterThrough, DirectionalPass }

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdUiElement : UdonSharpBehaviour
{
    public Collider target;
    public BirdUiPanel panel;
    public BirdUiActivation activation=BirdUiActivation.SelectThrough;
    public bool activeInBackground;
    public Vector3 localPassDirection=Vector3.up;
    [Range(0,180)] public float passAngle=45;
    public int priority;
    public UdonBehaviour actionTarget;
    public string actionEvent="Activate";
    [Tooltip("Optional local listener; read this element's state in the named event.")]
    public UdonBehaviour stateTarget;
    public string stateEvent="StateChanged";
    public Renderer feedback;
    public Color normalColor=new Color(.1f,.35f,.5f);
    public Color highlightColor=new Color(.4f,.8f,1);
    public Color pressedColor=Color.white;
    [HideInInspector] public int state; // inactive, enabled, highlighted, activated, background.
    [HideInInspector] public BirdUiPointer lastPointer;
    [HideInInspector] public float hitDistance;
    private bool hovered, pressing;
    private bool[] wasThrough=new bool[0];
    private MaterialPropertyBlock block;

    public bool Available()
    {
        if (!enabled || !gameObject.activeInHierarchy || target==null || !target.enabled || !target.gameObject.activeInHierarchy) return false;
        System.Type type=target.GetType();
        if(type==typeof(MeshCollider))
        {
            MeshCollider mesh=(MeshCollider)target;
            return mesh.convex && mesh.sharedMesh!=null;
        }
        return type==typeof(BoxCollider) || type==typeof(SphereCollider) || type==typeof(CapsuleCollider);
    }
    public bool Accepts(BirdUiPointer pointer)
    {
        return Available() && pointer!=null && pointer.IsTracked() &&
            (panel==null || (panel.Accepts(pointer) && (panel.state!=2 || activeInBackground)));
    }
    public void ResetPointers(int count) { wasThrough=new bool[count]; }
    public void BeginFrame() { hovered=pressing=false; }
    public bool Evaluate(BirdUiPointer pointer, int slot, bool fresh)
    {
        hitDistance=float.PositiveInfinity;
        if (!Accepts(pointer)) { wasThrough[slot]=false; return false; }
        Vector3 delta=pointer.position-pointer.origin;
        float range=delta.magnitude;
        RaycastHit hit;
        bool contains=Contains(pointer.position);
        bool through=contains || Contains(pointer.origin);
        if (range>.000001f && target.Raycast(new Ray(pointer.origin,delta/range),out hit,range))
        { through=true; hitDistance=hit.distance; }
        else if(through) hitDistance=0;
        hovered|=through; pressing|=through && pointer.pressed;
        bool prior=wasThrough[slot]; wasThrough[slot]=through;
        if (!fresh) return false;
        if (activation==BirdUiActivation.SelectAtPoint) return contains && pointer.pressedThisSample;
        if (activation==BirdUiActivation.SelectThrough) return through && pointer.pressedThisSample;
        if (activation==BirdUiActivation.EnterThrough) return through && !prior && pointer.hasHistory;
        if (activation!=BirdUiActivation.DirectionalPass || !pointer.hasHistory) return false;
        Vector3 motion=pointer.position-pointer.previousPosition;
        float length=motion.magnitude;
        if(length<.000001f) return false;
        Vector3 direction=transform.TransformDirection(localPassDirection).normalized;
        if(direction.sqrMagnitude<.5f || Vector3.Dot(motion/length,direction)<Mathf.Cos(passAngle*Mathf.Deg2Rad)) return false;
        if(!target.Raycast(new Ray(pointer.previousPosition,motion/length),out hit,length)) return false;
        hitDistance=hit.distance; return true;
    }
    public void Invoke(BirdUiPointer pointer)
    {
        if(!Accepts(pointer)) return;
        lastPointer=pointer;
        if(actionTarget!=null && !string.IsNullOrEmpty(actionEvent)) actionTarget.SendCustomEvent(actionEvent);
    }
    public void FinishFrame()
    {
        int previousState=state;
        if(!Available() || (panel!=null && (!panel.enabled || !panel.gameObject.activeInHierarchy || panel.state==0))) state=0;
        else if(panel!=null && panel.state==2 && !activeInBackground) state=4;
        else state=pressing ? 3 : hovered ? 2 : 1;
        if(feedback!=null)
        {
            if(block==null) block=new MaterialPropertyBlock();
            feedback.GetPropertyBlock(block);
            Color color=state==3 ? pressedColor : state==2 ? highlightColor : normalColor;
            if(state==0 || state==4) color*=.4f;
            block.SetColor("_Color",color); feedback.SetPropertyBlock(block);
        }
        if(previousState!=state && stateTarget!=null && !string.IsNullOrEmpty(stateEvent)) stateTarget.SendCustomEvent(stateEvent);
    }
    private bool Contains(Vector3 point) { return (target.ClosestPoint(point)-point).sqrMagnitude<1e-10f; }
    private void OnDisable()
    {
        for(int i=0;i<wasThrough.Length;i++) wasThrough[i]=false;
        hovered=pressing=false; lastPointer=null; FinishFrame();
    }
}
