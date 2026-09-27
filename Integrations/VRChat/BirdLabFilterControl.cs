using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// Laboratory comparison only. Both modes use the same input, geometry and view.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdLabFilterControl : UdonSharpBehaviour
{
    public BirdCursorState[] cursors;
    public Text label;
    public bool filtered = true;

    private void Start() { Apply(); }
    public override void Interact()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        filtered = !filtered; Apply();
    }
    public void SetRaw()
    { if (enabled && gameObject.activeInHierarchy) { filtered = false; Apply(); } }
    public void SetFiltered()
    { if (enabled && gameObject.activeInHierarchy) { filtered = true; Apply(); } }
    private void Apply()
    {
        if (cursors != null) foreach (var cursor in cursors) if (cursor != null && cursor.smoothing != filtered)
        {
            // A comparison starts from a fresh sample, without old mode history.
            cursor.Cancel(); cursor.smoothing = filtered;
        }
        if (label != null) label.text = filtered ? "Point / FILTERED\nPress for RAW" : "Point / RAW\nPress for FILTERED";
    }
}
