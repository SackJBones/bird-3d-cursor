using UdonSharp;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdAvatarControl : UdonSharpBehaviour
{
    public BirdAvatarInput left;
    public BirdAvatarInput right;
    public bool resetOnly;
    public Text feedback;
    public override void Interact()
    {
        if (resetOnly)
        {
            if (left != null) left.ResetCalibration();
            if (right != null) right.ResetCalibration();
            if (feedback != null) feedback.text = "Preview reset. Hold a comfortable pose, then calibrate.";
        }
        else
        {
            if (left != null) left.CalibrateNeutral();
            if (right != null) right.CalibrateNeutral();
            if (feedback != null) feedback.text = left != null && right != null && left.calibrated && right.calibrated
                ? "Both hands calibrated. Move your fingers to explore. Clicks disabled."
                : "Calibration incomplete. Check both hand-status labels and try again.";
        }
    }
}
