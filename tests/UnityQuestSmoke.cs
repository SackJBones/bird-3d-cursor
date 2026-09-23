using UnityEngine;
using UnityEngine.XR;

// Deployment smoke test only: synthetic Bird poses, real headset view tracking.
public sealed class UnityQuestSmoke : MonoBehaviour
{
    private Camera view;
    private Vector3 initialHead;
    private bool centered;
    private float nextLog;
    private void Start()
    {
        view = FindObjectOfType<Camera>();
        var label = new GameObject("Smoke test label").AddComponent<TextMesh>();
        label.text = "BIRD / QUEST TEST\nSynthetic trails - no hand tracking";
        label.fontSize = 48;
        label.characterSize = 0.003f;
        label.anchor = TextAnchor.MiddleCenter;
        label.transform.position = new Vector3(0, 0.3f, 0.2f);
    }
    private void LateUpdate()
    {
        if (view == null) return;
        Vector3 head = InputTracking.GetLocalPosition(XRNode.Head);
        if (!centered && XRSettings.isDeviceActive) { initialHead = head; centered = true; }
        view.transform.position = new Vector3(0, 0.05f, -0.9f) + head - initialHead;
        view.transform.rotation = InputTracking.GetLocalRotation(XRNode.Head);
        if (Time.unscaledTime >= nextLog)
        {
            Debug.Log("BIRD_QUEST_SMOKE: xr=" + XRSettings.isDeviceActive + " device=" + XRSettings.loadedDeviceName);
            nextLog = Time.unscaledTime + 10;
        }
    }
}
