#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
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
    private static ClientSimPlayerAvatarManager fixtureManager;
    private static FieldInfo animatorField;
    private static Animator savedAnimator;
    private static int baselineLeft, baselineRight;
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

    public static void RunMissingBones()
    {
        Begin(false, true);
    }

    private static bool Automatic { get { return SessionState.GetBool("Bird.ClientSim.Automatic", true); } }
    private static bool MissingBones { get { return SessionState.GetBool("Bird.ClientSim.MissingBones", false); } }
    private static string ResultPath { get { return MissingBones ? "clientsim-probe-missing-result.txt" : Automatic ? "clientsim-probe-result.txt" : "clientsim-probe-explicit-result.txt"; } }

    private static void Begin(bool automatic, bool missingBones = false)
    {
        SessionState.SetBool("Bird.ClientSim.Automatic", automatic);
        SessionState.SetBool("Bird.ClientSim.MissingBones", missingBones);
        deadline = nextCheck = 0;
        stage = 0;
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
            if (markers.Length != 32) throw new Exception("Expected exactly 32 probe markers");
            if (MissingBones && stage == 1)
            {
                var bones = (int[])backing.GetProgramVariable("bones");
                foreach (int bone in bones)
                    if (Networking.LocalPlayer.GetBonePosition((HumanBodyBones)bone) != Vector3.zero)
                        throw new Exception("Missing-avatar fixture did not return the SDK zero sentinel");
                foreach (var marker in markers)
                    if (marker.gameObject.activeSelf) throw new Exception("Missing bones left stale active markers");
                if ((int)backing.GetProgramVariable("leftAvailable") != 0 || (int)backing.GetProgramVariable("rightAvailable") != 0 ||
                    !label.text.Contains("Left 0/16  Right 0/16"))
                    throw new Exception("Missing bones left stale counts or label");
                RestoreAvatar();
                stage = 2;
                nextCheck = EditorApplication.timeSinceStartup + 0.5;
                return;
            }
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
                if (stage == 0) File.WriteAllText("clientsim-probe-baseline-result.txt", "PASS: live ClientSim/Udon label and marker-count agreement; " + observed + ". Lifecycle and missing-bone checks are separate.");
                if (MissingBones)
                {
                    if (stage == 2)
                    {
                        if (left != baselineLeft || right != baselineRight)
                            throw new Exception("Restored avatar did not recover baseline availability");
                        Finish(true, observed + "; controlled missing-avatar fixture returned zero for all 32 bones, cleared markers/counts/label, and recovered baseline. No avatar-switch, hardware or scale validation.");
                        return;
                    }
                    if (left == 0 || right == 0) throw new Exception("Missing-bone test requires a nonempty baseline on both hands");
                    baselineLeft = left; baselineRight = right;
                    // SDK 3.10.5 returns zero when this runtime-only animator reference is absent.
                    // No SDK source/asset edits and no calls to the probe's C# Update.
                    fixtureManager = Networking.LocalPlayer.GetClientSimPlayer().GetAvatarDataProvider() as ClientSimPlayerAvatarManager;
                    if (fixtureManager == null) throw new Exception("Unexpected local avatar provider");
                    animatorField = typeof(ClientSimPlayerAvatarManager).GetField("avatarAnimator", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (animatorField == null) throw new Exception("SDK fixture field changed");
                    savedAnimator = animatorField.GetValue(fixtureManager) as Animator;
                    if (savedAnimator == null) throw new Exception("Fixture has no animator to restore");
                    animatorField.SetValue(fixtureManager, null);
                    stage = 1;
                    nextCheck = EditorApplication.timeSinceStartup + 0.5;
                    return;
                }
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
        RestoreAvatar();
        SessionState.SetBool(Active, false);
        File.WriteAllText(ResultPath, (success ? "PASS: " : "FAIL: ") + detail + (success ? "" : " Observed: " + observed));
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void RestoreAvatar()
    {
        if (fixtureManager != null && animatorField != null && savedAnimator != null)
            animatorField.SetValue(fixtureManager, savedAnimator);
        fixtureManager = null;
        animatorField = null;
        savedAnimator = null;
    }
}
#endif
