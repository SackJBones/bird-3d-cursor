using UnityEngine;

namespace Bird3DCursor.Manipulation
{
    /// <summary>Optional UnityEvent-friendly commands. Input gestures remain the host's choice.</summary>
    [AddComponentMenu("Bird/Manipulation/Held Pose Controls")]
    public sealed class BirdHeldPoseControls : MonoBehaviour
    {
        public BirdGrabInteractor interactor;
        public void RotateAroundWorkspaceUp(float degrees)
        {
            if(!isActiveAndEnabled || interactor==null || interactor.ActiveTarget==null || !BirdPlacementRegion.Finite(degrees)) return;
            Transform item=interactor.ActiveTarget.transform;
            Quaternion parent=BirdPlacementRegion.AuthoredRotation(item.parent);
            Quaternion delta=Quaternion.AngleAxis(degrees,BirdPlacementRegion.AuthoredRotation(interactor.ActiveTarget.Region.transform)*Vector3.up);
            interactor.TrySetHeldPose(Quaternion.Inverse(parent)*delta*parent*interactor.RequestedLocalRotation,interactor.RequestedScaleFactor);
        }
        public void ResizeBy(float multiplier)
        {
            if(!isActiveAndEnabled || interactor==null || !BirdPlacementRegion.Finite(multiplier) || multiplier<=0) return;
            interactor.TrySetHeldPose(interactor.RequestedLocalRotation,interactor.RequestedScaleFactor*multiplier);
        }
        public void ResetPose() { if(isActiveAndEnabled && interactor!=null) interactor.ResetHeldPose(); }
    }
}
