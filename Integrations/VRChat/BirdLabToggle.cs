using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// Ordinary VRChat Interact control. Local diagnostic state is deliberately not synchronized.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdLabToggle : UdonSharpBehaviour
{
    public GameObject target;
    public Text label;
    public string title = "Mirror";
    public bool initiallyEnabled;

    private void Start()
    {
        if (target != null) target.SetActive(initiallyEnabled);
        Refresh();
    }

    public override void Interact()
    {
        if (target != null) target.SetActive(!target.activeSelf);
        Refresh();
    }

    private void Refresh()
    {
        if (label != null) label.text = title + (target != null && target.activeSelf ? " / ON" : " / OFF");
    }
}
