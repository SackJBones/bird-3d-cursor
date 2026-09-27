using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// Local laboratory comparison; changing origin preserves fingertip calibration.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdLabRootControl : UdonSharpBehaviour
{
    public BirdAvatarHandInput[] inputs;
    public Text label;
    public bool centered=true;
    private void Start() { Apply(); }
    public override void Interact()
    { if(enabled && gameObject.activeInHierarchy) { centered=!centered; Apply(); } }
    public void SetClassic()
    { if(enabled && gameObject.activeInHierarchy) { centered=false; Apply(); } }
    public void SetPalm()
    { if(enabled && gameObject.activeInHierarchy) { centered=true; Apply(); } }
    private void Apply()
    {
        float share=centered?.5f:0;
        if(inputs!=null) foreach(var input in inputs) if(input!=null && input.littleFingerRootShare!=share)
        {
            // Immediate cancellation also covers a mode round-trip occurring
            // between two observed avatar/UI frames.
            if(input.cursor!=null) input.cursor.Cancel();
            input.littleFingerRootShare=share;
        }
        if(label!=null) label.text=centered?"Origin / PALM\nPress for CLASSIC":"Origin / CLASSIC\nPress for PALM";
    }
}
