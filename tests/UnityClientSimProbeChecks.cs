#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;

// Play-mode observer: assertions read the live Udon heap, never call the C# proxy Update.
public static class UnityClientSimProbeChecks
{
    private const string Active = "Bird.ClientSim.Active";
    private static double deadline;
    private static double nextCheck;
    private static int stage;
    private static string observed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartFrameDriver()
    {
        if (SessionState.GetBool(Active, false))
            new GameObject("Bird ClientSim frame checks").AddComponent<UnityClientSimFrameDriver>();
    }

    public static void Run()
    {
        Begin(true);
    }

    public static void RunExplicit()
    {
        Begin(false);
    }

    private static bool Automatic { get { return SessionState.GetBool("Bird.ClientSim.Automatic", true); } }
    private static string ResultPath { get { return Automatic ? "clientsim-probe-result.txt" : "clientsim-probe-explicit-result.txt"; } }

    private static void Begin(bool automatic)
    {
        SessionState.SetBool("Bird.ClientSim.Automatic", automatic);
        File.WriteAllText(ResultPath, "PENDING");
        File.WriteAllText("clientsim-probe-baseline-result.txt", "PENDING");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer)
            throw new Exception("Enable ClientSim and spawnPlayer before running this check; global preferences are not changed.");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdHandProbe.unity");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error.");
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    public static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timed out waiting for ClientSim/Udon probe"); return; }
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        try
        {
            var proxy = UnityEngine.Object.FindObjectOfType<BirdHandDataProbe>(true);
            if (proxy == null) return;
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            if (backing == null || !Utilities.IsValid(Networking.LocalPlayer)) return;
            var label = (UnityEngine.UI.Text)backing.GetProgramVariable("status");
            var markers = (Transform[])backing.GetProgramVariable("markers");
            if (label == null || markers == null) return;
            if (stage == 0 || stage == 2)
            {
                if (!label.text.Contains("AVATAR BONE PROBE")) return;
                if (!ClientSimMain.HasInstance()) throw new Exception("ClientSim instance missing");
                int left = (int)backing.GetProgramVariable("leftAvailable");
                int right = (int)backing.GetProgramVariable("rightAvailable");
                int visibleLeft = 0, visibleRight = 0;
                for (int i = 0; i < markers.Length; i++)
                {
                    if (markers[i] == null) throw new Exception("Missing marker");
                    if (!markers[i].gameObject.activeSelf) continue;
                    if (i < 16) visibleLeft++; else visibleRight++;
                    if (markers[i].position == Vector3.zero) throw new Exception("Visible marker at missing-bone sentinel");
                }
                if (left != visibleLeft || right != visibleRight || left < 0 || left > 16 || right < 0 || right > 16)
                    throw new Exception("Live Udon counts disagree with visible markers");
                observed = "left=" + left + "/16 right=" + right + "/16 VR=" + Networking.LocalPlayer.IsUserInVR();
                if (stage == 0) File.WriteAllText("clientsim-probe-baseline-result.txt", "PASS: live ClientSim/Udon label and marker-count agreement; " + observed + ". Lifecycle check is separate.");
                if (stage == 2) { Finish(true, observed + (Automatic ? "; automatic disable/re-enable" : "; explicit Udon PauseProbe/ResumeProbe") + " clearing and recovery checked. No hardware or scale validation."); return; }
                // Disable the object: the UdonSharp editor synchronizes component enabled state
                // with its proxy, so toggling only the backing component is not a stable test.
                if (Automatic) proxy.gameObject.SetActive(false);
                else backing.SendCustomEvent("PauseProbe");
                stage = 1;
                nextCheck = EditorApplication.timeSinceStartup + 0.5;
            }
            else
            {
                foreach (var marker in markers) if (marker.gameObject.activeSelf)
                {
                    if (!Automatic) throw new Exception("PauseProbe left a marker active");
                    bool dispatched = backing.RunEvent("_onDisable");
                    int activeAfter = 0;
                    foreach (var candidate in markers) if (candidate.gameObject.activeSelf) activeAfter++;
                    throw new Exception("Automatic disable left active marker flags and stale counts; object=" + proxy.gameObject.activeSelf +
                        ". Diagnostic direct Udon event dispatch=" + dispatched + " remainingActive=" + activeAfter +
                        " left=" + backing.GetProgramVariable("leftAvailable") + " label=" + label.text +
                        ". Direct dispatch does not satisfy the automatic lifecycle check.");
                }
                if ((int)backing.GetProgramVariable("leftAvailable") != 0 || (int)backing.GetProgramVariable("rightAvailable") != 0 || !label.text.Contains(Automatic ? "Disabled" : "Paused"))
                    throw new Exception("Disable event did not clear Udon state");
                if (Automatic) proxy.gameObject.SetActive(true);
                else backing.SendCustomEvent("ResumeProbe");
                stage = 2;
                nextCheck = EditorApplication.timeSinceStartup + 0.5;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    private static void Finish(bool success, string detail)
    {
        SessionState.SetBool(Active, false);
        File.WriteAllText(ResultPath, (success ? "PASS: " : "FAIL: ") + detail + (success ? "" : " Observed: " + observed));
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
