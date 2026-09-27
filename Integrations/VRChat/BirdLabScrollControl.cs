using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// Lab-only native reset and explanatory text. Interaction remains in the
// reusable router and spherical-scroll components, independent of this view.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(200)]
public class BirdLabScrollControl : UdonSharpBehaviour
{
    public BirdUiSphericalScroll scroll;
    public BirdUiPointer left, right;
    public Text status;
    private Quaternion initialRotation;
    private bool initialized;
    private float nextText;

    private void Start()
    {
        if(scroll != null && scroll.rotationTarget != null)
        { initialRotation=scroll.rotationTarget.localRotation; initialized=true; }
    }
    public override void Interact()
    {
        if(!enabled || !gameObject.activeInHierarchy || scroll == null || !initialized) return;
        scroll.Cancel();
        if(scroll.rotationTarget != null) scroll.rotationTarget.localRotation=initialRotation;
        Physics.SyncTransforms(); nextText=0;
    }
    public override void PostLateUpdate()
    {
        if(!enabled || !gameObject.activeInHierarchy || status==null || Time.unscaledTime<nextText) return;
        nextText=Time.unscaledTime+.2f;
        if(scroll==null) status.text="Reach station unavailable";
        else if(scroll.activePointer!=null)
            status.text=(scroll.activePointer==left?"LEFT":"RIGHT")+" HAND / SPINNING\nWithdraw into the sphere to coast";
        else if((left==null || !left.IsTracked()) && (right==null || !right.IsTracked()))
            status.text="SET an open hand at the console first";
        else if(scroll.angularVelocity.sqrMagnitude>.0001f)
            status.text="COASTING\nMove Bird inside, then extend through the back";
        else status.text="Move Bird inside, then extend through the back\nSweep to spin. Colors highlight; clicks are disabled.";
    }
}
