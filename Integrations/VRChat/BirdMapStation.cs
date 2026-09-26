using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(150)]
public class BirdMapStation : UdonSharpBehaviour
{
    public BirdUiRangeScale zoom;
    public BirdUiSphericalScroll rotation;
    public Transform rotationTarget;
    public Quaternion restRotation=Quaternion.Euler(-30,-20,0);
    public Text label;
    [HideInInspector] public bool zoomMode;
    private void OnEnable() { RotateMap(); }
    private void OnDisable() { if(zoom!=null) zoom.StopScaling(); if(rotation!=null) rotation.Cancel(); }
    public void RotateMap() { if(zoom!=null) zoom.StopScaling(); if(rotation!=null) rotation.enabled=true; zoomMode=false; }
    public void ZoomMap() { if(rotation!=null) rotation.enabled=false; if(zoom!=null) zoom.StartScaling(); zoomMode=true; }
    public void ResetMap() { if(zoom!=null) zoom.ResetScale(); if(rotation!=null) rotation.Cancel(); if(rotationTarget!=null) rotationTarget.localRotation=restRotation; RotateMap(); Physics.SyncTransforms(); }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if(label==null || zoom==null) return;
        label.text=zoomMode?"ZOOM / point through, vary reach; withdraw to release\n"+zoom.factor.ToString("0.00")+"x":
            "ROTATE / pierce the BACK, flick; withdraw to coast";
    }
}
