using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Deliberately labeled desktop demonstration. It is not a hand-tracking adapter.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdUiDesktopInput : UdonSharpBehaviour
{
    public BirdUiPointer pointer;
    public Transform marker;
    public Text label;
    public float range=3;
    public bool inputEnabled=true;
    private void Update()
    {
        if(pointer==null) return;
        VRCPlayerApi player=Networking.LocalPlayer;
        if(!inputEnabled || !Utilities.IsValid(player) || player.IsUserInVR())
        { pointer.Cancel(); if(marker!=null) marker.gameObject.SetActive(false); return; }
        VRCPlayerApi.TrackingData head=player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        range=Mathf.Clamp(range+Input.GetAxisRaw("Mouse ScrollWheel")*1.5f,.1f,30);
        pointer.sampleOrigin=head.position;
        pointer.samplePosition=head.position+head.rotation*Vector3.forward*range;
        pointer.sampleTracked=true; pointer.samplePressed=Input.GetMouseButton(0);
        pointer.Submit();
        if(marker!=null) { marker.gameObject.SetActive(true); marker.position=pointer.position; }
        if(label!=null) label.text="DESKTOP INPUT / Look to aim, wheel to reach, click to choose\nReach through the open control. Inside sphere: choose. Beyond back: flick.";
    }
    private void OnDisable() { if(pointer!=null) pointer.Cancel(); if(marker!=null) marker.gameObject.SetActive(false); }
}
