using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

/// <summary>Optional command adapter. The grip remains the sole writer of the held pose.</summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdObjectPoseControls : UdonSharpBehaviour
{
    public BirdObjectGrip grip;
    public float rotationStep=15;
    public float scaleStep=1.25f;
    public bool desktopInput;
    public Text status;

    public void RotateLeft() { Rotate(-rotationStep); }
    public void RotateRight() { Rotate(rotationStep); }
    public void Grow() { Resize(scaleStep); }
    public void Shrink() { Resize(1/scaleStep); }
    public void ResetPose() { if(grip!=null) grip.ResetHeldPose(); }

    private void Rotate(float degrees)
    {
        if(grip==null || grip.ActiveTarget==null || grip.IsReturning) return;
        Transform parent=grip.ActiveTarget.transform.parent;
        BirdObjectRegion region=grip.ActiveTarget.region;
        Quaternion frame=region.AuthoredRotation(parent);
        Quaternion turn=Quaternion.AngleAxis(degrees,region.AuthoredRotation(region.transform)*Vector3.up);
        grip.poseRequestRotation=Quaternion.Inverse(frame)*turn*frame*grip.RequestedLocalRotation;
        grip.poseRequestFactor=grip.RequestedScaleFactor;
        grip.RequestPose();
    }
    private void Resize(float multiplier)
    {
        if(grip==null || grip.ActiveTarget==null || grip.IsReturning) return;
        grip.poseRequestRotation=grip.RequestedLocalRotation;
        grip.poseRequestFactor=grip.RequestedScaleFactor*multiplier;
        grip.RequestPose();
    }
    private void Update()
    {
        if(!desktopInput || !Utilities.IsValid(Networking.LocalPlayer) || Networking.LocalPlayer.IsUserInVR()) return;
        if(Input.GetKeyDown(KeyCode.Q)) RotateLeft();
        if(Input.GetKeyDown(KeyCode.E)) RotateRight();
        if(Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) Grow();
        if(Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Shrink();
        if(Input.GetKeyDown(KeyCode.R)) ResetPose();
    }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if(status==null || grip==null) return;
        string action=grip.ActiveTarget==null?"HOLD A TOWER SECTION":grip.IsReturning?"RETURNING":grip.ReadyToPlace?"RELEASE TO PLACE":grip.PoseLimited?"POSE LIMIT / ADJUST OR RESET":"MATCH THE OUTLINE";
        status.text=action+"\n\nLocal desktop pose demo\nLook / wheel / hold to move\nQ / E turn 15 degrees\n- / + change size\nR restores held pose\nX cancels";
    }
}
