using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(150)]
public class BirdObjectFeedback : UdonSharpBehaviour
{
    public BirdObjectGrip grip;
    public BirdObjectTarget target;
    public Renderer visual;
    public Color hoverTint=new Color(.3f,.4f,.4f),heldTint=new Color(.4f,.4f,.3f);
    private Renderer bound;
    private MaterialPropertyBlock saved,block;
    private Color baseline;
    private int last=-1;
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if(!enabled || !gameObject.activeInHierarchy) return;
        if(bound!=visual) Restore();
        if(visual==null || visual.sharedMaterial==null || grip==null || target==null) return;
        if(bound==null)
        {
            bound=visual; saved=new MaterialPropertyBlock(); block=new MaterialPropertyBlock();
            visual.GetPropertyBlock(saved); baseline=visual.sharedMaterial.color;
        }
        int state=!target.enabled || !target.gameObject.activeInHierarchy || !grip.enabled?0:grip.ActiveTarget==target?(grip.IsReturning?3:grip.ReadyToPlace?4:2):grip.HoveredTarget==target?1:0;
        if(last==state) return; last=state; visual.GetPropertyBlock(block);
        Color tint=state==1?hoverTint:state==2?heldTint:state==3?new Color(.3f,.05f,.05f):state==4?new Color(.1f,.6f,.15f):Color.black;
        block.SetColor("_Color",new Color(Mathf.Clamp01(baseline.r+tint.r),Mathf.Clamp01(baseline.g+tint.g),Mathf.Clamp01(baseline.b+tint.b),baseline.a)); visual.SetPropertyBlock(block);
    }
    private void OnDisable() { Restore(); }
    private void Restore() { if(bound!=null && saved!=null) bound.SetPropertyBlock(saved); bound=null; saved=null; block=null; last=-1; }
}
