#if UNITY_EDITOR
using UnityEngine;

// Generated validation-project helper only; never included in a client build.
public sealed class UnityClientSimFrameDriver : MonoBehaviour
{
    private void Update() { UnityClientSimProbeChecks.Tick(); }
}
#endif
