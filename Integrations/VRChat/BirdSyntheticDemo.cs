using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// Explicitly synthetic input for a visible world demo, not a hand tracking adapter.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdSyntheticDemo : UdonSharpBehaviour
{
    public BirdCursorState cursor;
    public LineRenderer trail;
    public Renderer marker;
    public Material idleMaterial;
    public Material pressedMaterial;
    public Text label;
    public float phase;
    public string handLabel;
    [HideInInspector] public int clicks;
    private Vector3[] samples = new Vector3[4];
    private Vector3[] history = new Vector3[64];
    private int count;
    private float nextSample;
    private bool paused;

    private void Update()
    {
        if (paused || cursor == null || trail == null || Time.time < nextSample) return;
        nextSample = Time.time + 0.03f;
        float t = Time.time + phase;
        Vector3 root = transform.position;
        Vector3 direction = new Vector3(Mathf.Sin(t) * 0.65f, 0.4f + Mathf.Cos(t * 0.7f) * 0.35f, 1).normalized;
        Vector3 center = root + direction * (0.035f + 0.009f * Mathf.Sin(t * 0.8f));
        float r = 0.03f / Mathf.Sqrt(3);
        samples[0] = center + new Vector3(r, r, r);
        samples[1] = center + new Vector3(r, -r, -r);
        samples[2] = center + new Vector3(-r, r, -r);
        samples[3] = center + new Vector3(-r, -r, r);
        cursor.points = samples;
        cursor.handRoot = root;
        cursor.indexTip = center + Vector3.right * (0.03f - (0.0055f + 0.004f * Mathf.Sin(t * 2)));
        cursor.tracking = true;
        cursor.Step();
        if (!cursor.poseValid) { count = 0; trail.positionCount = 0; return; }
        if (cursor.down) clicks++;
        if (marker != null) marker.sharedMaterial = cursor.selected ? pressedMaterial : idleMaterial;
        if (count < 64) count++;
        for (int i = count - 1; i > 0; i--) history[i] = history[i - 1];
        history[0] = cursor.position;
        trail.positionCount = count;
        for (int i = 0; i < count; i++) trail.SetPosition(i, history[i]);
        if (label != null) label.text = handLabel + " / SYNTHETIC\n" + (cursor.selected ? "PRESSED" : "OPEN") + "   Clicks: " + clicks;
    }

    public void PauseDemo()
    {
        paused = true;
        if (cursor != null) cursor.Cancel();
        count = 0;
        if (trail != null) trail.positionCount = 0;
        if (label != null) label.text = handLabel + " / SYNTHETIC\nPaused";
    }
    public void ResumeDemo() { paused = false; nextSample = 0; }
}
