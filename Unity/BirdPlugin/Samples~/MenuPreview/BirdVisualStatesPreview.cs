using Bird3DCursor.UI;
using UnityEngine;

namespace Bird3DCursor.Samples
{
    /// <summary>Optional pose-based artwork on the conventional nested menu sample.</summary>
    [RequireComponent(typeof(BirdMenuPreview))]
    public sealed class BirdVisualStatesPreview : MonoBehaviour
    {
        bool ready;
        public void Initialize()
        {
            if(ready) return; ready=true;
            var demo=GetComponent<BirdMenuPreview>(); demo.Initialize();
            foreach(var element in demo.GetComponentsInChildren<BirdMenuElement>(true))
            {
                var simple=element.GetComponent<BirdMenuFeedback>(); simple.enabled=false;
                var body=element.GetComponentInChildren<MeshRenderer>(true);
                var text=element.GetComponentInChildren<TextMesh>(true);
                var branch=new GameObject("Visual state artwork").transform; branch.SetParent(element.transform,false);
                body.transform.SetParent(branch,true); if(text!=null) text.transform.SetParent(branch,true);
                var visual=element.gameObject.AddComponent<BirdMenuVisual>(); visual.Configure(element,branch,body);
                visual.SetAppearance(BirdMenuVisualState.Inactive,new BirdMenuAppearance { visible=false,scaleMultiplier=Vector3.one*.85f,color=new Color(.04f,.08f,.12f) });
                visual.SetAppearance(BirdMenuVisualState.Highlighted,new BirdMenuAppearance { positionOffset=Vector3.back*.045f,rotationOffset=new Vector3(0,4,0),scaleMultiplier=Vector3.one*1.06f,color=new Color(.1f,.85f,1) });
                visual.SetAppearance(BirdMenuVisualState.Activated,new BirdMenuAppearance { positionOffset=Vector3.forward*.025f,scaleMultiplier=Vector3.one*.95f,color=new Color(1,.65f,.2f) });
                visual.SetAppearance(BirdMenuVisualState.Background,new BirdMenuAppearance { positionOffset=Vector3.forward*.08f,scaleMultiplier=Vector3.one*.93f,color=new Color(.06f,.12f,.18f) });
            }
        }
        void Start() { Initialize(); }
    }
}
