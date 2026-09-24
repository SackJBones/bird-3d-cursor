using UdonSharp;
using UnityEngine.UI;

// Conventional local world interaction, independent of synthetic cursor input.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdDemoControl : UdonSharpBehaviour
{
    public BirdSyntheticDemo left;
    public BirdSyntheticDemo right;
    public Text label;
    public bool clearOnly;
    private bool paused;

    public override void Interact()
    {
        if (clearOnly)
        {
            if (left != null) left.ClearTrail();
            if (right != null) right.ClearTrail();
            return;
        }
        paused = !paused;
        if (paused)
        {
            if (left != null) left.PauseDemo();
            if (right != null) right.PauseDemo();
        }
        else
        {
            if (left != null) left.ResumeDemo();
            if (right != null) right.ResumeDemo();
        }
        if (label != null) label.text = paused ? "RESUME" : "PAUSE";
    }
}
