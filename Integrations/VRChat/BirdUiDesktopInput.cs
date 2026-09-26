using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

// Deliberately labeled desktop demonstration. It is not a hand-tracking adapter.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdUiDesktopInput : UdonSharpBehaviour
{
    public BirdUiPointer pointer;
    public Transform marker;
    public Text label;
    public float range=3;
    public bool inputEnabled=true;
    public float maximumRange=30;
    public bool exponentialRange;
    public UdonBehaviour cancelTarget;
    public string cancelEvent="Cancel";
    [TextArea] public string instructions="DESKTOP INPUT / Look to aim, wheel to reach, click to choose\nReach through the open control. Inside sphere: choose. Beyond back: flick.";
    private void Update()
    {
        if(pointer==null) return;
        VRCPlayerApi player=Networking.LocalPlayer;
        if(!inputEnabled || !Utilities.IsValid(player) || player.IsUserInVR())
        { pointer.Cancel(); if(marker!=null) marker.gameObject.SetActive(false); return; }
        VRCPlayerApi.TrackingData head=player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        float wheel=Input.GetAxisRaw("Mouse ScrollWheel");
        range=Mathf.Clamp(exponentialRange?range*Mathf.Exp(wheel*1.4f):range+wheel*1.5f,.1f,Mathf.Max(.1f,maximumRange));
        pointer.sampleOrigin=head.position;
        pointer.samplePosition=head.position+head.rotation*Vector3.forward*range;
        pointer.sampleTracked=true; pointer.samplePressed=Input.GetMouseButton(0);
        pointer.Submit();
        if(Input.GetKeyDown(KeyCode.X) && cancelTarget!=null && !string.IsNullOrEmpty(cancelEvent)) cancelTarget.SendCustomEvent(cancelEvent);
        if(marker!=null) { marker.gameObject.SetActive(true); marker.position=pointer.position; }
        if(label!=null) label.text=instructions;
    }
    private void OnDisable() { if(pointer!=null) pointer.Cancel(); if(marker!=null) marker.gameObject.SetActive(false); }
}
