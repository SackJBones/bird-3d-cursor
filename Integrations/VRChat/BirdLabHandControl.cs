using UdonSharp;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdLabHandControl : UdonSharpBehaviour
{
    public BirdAvatarHandInput input;
    public bool reset;
    public override void Interact()
    {
        if (input == null) return;
        if (reset) input.ResetCalibration(); else input.CalibrateOpenHand();
    }
}
