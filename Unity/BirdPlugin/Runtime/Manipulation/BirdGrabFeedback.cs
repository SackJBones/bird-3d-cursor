using UnityEngine;

namespace Bird3DCursor.Manipulation
{
    /// <summary>Optional renderer feedback, separate from volume, ownership and placement rules.</summary>
    [AddComponentMenu("Bird/Manipulation/Grab Feedback")]
    [DefaultExecutionOrder(150)]
    public sealed class BirdGrabFeedback : MonoBehaviour
    {
        public BirdGrabInteractor interactor;
        public BirdGrabTarget target;
        public Renderer visual;
        public Color hoverTint=new Color(.3f,.4f,.4f);
        public Color heldTint=new Color(.4f,.4f,.3f);
        MaterialPropertyBlock saved,block;
        Renderer bound;
        Color baseline;
        int last=-1;
        void OnEnable() { last=-1; }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if(!isActiveAndEnabled) return;
            if(bound!=visual) Restore();
            if(visual==null || visual.sharedMaterial==null || interactor==null || target==null) return;
            if(bound==null)
            {
                bound=visual;
                saved=new MaterialPropertyBlock(); block=new MaterialPropertyBlock();
                visual.GetPropertyBlock(saved); baseline=visual.sharedMaterial.color;
            }
            int state=!target.isActiveAndEnabled || !interactor.isActiveAndEnabled?0:interactor.ActiveTarget==target?(interactor.IsReturning?3:interactor.ReadyToPlace?4:2):interactor.HoveredTarget==target?1:0;
            if(last==state) return; last=state;
            visual.GetPropertyBlock(block);
            Color tint=state==1?hoverTint:state==2?heldTint:state==3?new Color(.3f,.05f,.05f):state==4?new Color(.1f,.6f,.15f):Color.black;
            block.SetColor("_Color",new Color(Mathf.Clamp01(baseline.r+tint.r),Mathf.Clamp01(baseline.g+tint.g),Mathf.Clamp01(baseline.b+tint.b),baseline.a));
            visual.SetPropertyBlock(block);
        }
        void OnDisable() { Restore(); }
        void Restore() { if(bound!=null && saved!=null) bound.SetPropertyBlock(saved); bound=null; saved=null; block=null; last=-1; }
    }
}
