#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;

public class UnityAvatarInputChecks : MonoBehaviour
{
    private const string Active = "Bird.AvatarInput.Checks";
    private static double deadline;
    private static float next;
    private static int stage;
    private static ClientSimPlayerAvatarManager manager;
    private static FieldInfo field;
    private static Animator animator;
    private static string observed;
    public static void Run()
    {
        File.WriteAllText("udon-avatar-result.txt", "PENDING");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer) throw new Exception("ClientSim required");
        const string path = "Assets/BirdGenerated/BirdAvatarInput.asset";
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if (program == null)
        {
            program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdAvatarInput.cs");
            if (program.sourceCsScript == null) throw new Exception("Restore adapter source/meta");
            AssetDatabase.CreateAsset(program, path); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
        for (int i = 0; i < 2; i++)
        {
            var input = new GameObject("Avatar input " + i).AddUdonSharpComponent<BirdAvatarInput>();
            input.rightHand = i == 1;
            input.cursor = new GameObject("Avatar cursor " + i).AddUdonSharpComponent<BirdCursorState>();
            input.cursor.fitter = new GameObject("Avatar fit " + i).AddUdonSharpComponent<BirdSphereFit>();
            input.cursor.cursorVisual = new GameObject("Avatar marker " + i).transform;
            input.cursor.smoothing = true;
            UdonSharpEditorUtility.CopyProxyToUdon(input.cursor);
            UdonSharpEditorUtility.CopyProxyToUdon(input);
        }
        SessionState.SetBool(Active, true); EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (SessionState.GetBool(Active, false)) new GameObject("Avatar input checks").AddComponent<UnityAvatarInputChecks>();
    }
    private void Update()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Avatar input timeout");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad < 3 || Time.time < next) return;
            observed = "";
            var inputs = FindObjectsOfType<BirdAvatarInput>();
            if (inputs.Length != 2) throw new Exception("Expected two inputs");
            foreach (var proxy in inputs)
            {
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                var cursor = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy.cursor);
                bool ready = (bool)vm.GetProgramVariable("dataReady");
                if ((bool)cursor.GetProgramVariable("clicksAllowed") || (bool)cursor.GetProgramVariable("selected") || (bool)cursor.GetProgramVariable("down")) throw new Exception("Avatar approximation enabled clicks");
                if (stage == 1)
                {
                    if (ready || (int)vm.GetProgramVariable("available") != 0 || (bool)cursor.GetProgramVariable("poseValid") || ((Transform)cursor.GetProgramVariable("cursorVisual")).gameObject.activeSelf)
                        throw new Exception("Missing avatar did not cancel input/cursor");
                    continue;
                }
                if (!ready || (int)vm.GetProgramVariable("available") != 14) throw new Exception("Expected full default-avatar data");
                var points = (Vector3[])cursor.GetProgramVariable("points");
                string prefix = proxy.rightHand ? "Right" : "Left";
                string[] names = { "ThumbIntermediate", "ThumbDistal", "IndexProximal", "MiddleProximal", "MiddleIntermediate", "MiddleDistal", "RingProximal", "RingIntermediate", "RingDistal", "LittleProximal", "LittleIntermediate", "LittleDistal" };
                if (points.Length != 12) throw new Exception("Expected 12 fit bone origins");
                for (int i = 0; i < 12; i++)
                    if (Vector3.Distance(points[i], Networking.LocalPlayer.GetBonePosition((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), prefix + names[i]))) > 0.002f) throw new Exception("Bone mapping mismatch");
                Vector3 root = points[2] * 0.6f + Networking.LocalPlayer.GetBonePosition((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), prefix + "ThumbProximal")) * 0.4f;
                if (Vector3.Distance(root, (Vector3)cursor.GetProgramVariable("handRoot")) > 0.002f) throw new Exception("Weighted root mismatch");
                if (Vector3.Distance((Vector3)cursor.GetProgramVariable("indexTip"), Networking.LocalPlayer.GetBonePosition((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), prefix + "IndexDistal"))) > 0.002f) throw new Exception("Distal placeholder mismatch");
                // Availability does not promise a well-conditioned sphere for a particular avatar.
                bool valid = (bool)cursor.GetProgramVariable("poseValid");
                bool rangeRejected = (bool)vm.GetProgramVariable("rangeRejected");
                float measured = (float)vm.GetProgramVariable("measuredRange");
                float maximum = (float)vm.GetProgramVariable("maximumPreviewRange");
                if (measured > maximum && (!rangeRejected || valid)) throw new Exception("Excessive preview range was not rejected");
                if (((Transform)cursor.GetProgramVariable("cursorVisual")).gameObject.activeSelf != valid) throw new Exception("Fit validity/marker mismatch");
                float range = ((Vector3)cursor.GetProgramVariable("position") - root).magnitude;
                if (valid && (float.IsNaN(range) || float.IsInfinity(range))) throw new Exception("Nonfinite accepted cursor");
                observed += prefix + " available=14 cursorValid=" + valid + " rangeRejected=" + rangeRejected + " measuredRange=" + measured + "; ";
            }
            if (stage == 0)
            {
                manager = Networking.LocalPlayer.GetClientSimPlayer().GetAvatarDataProvider() as ClientSimPlayerAvatarManager;
                field = typeof(ClientSimPlayerAvatarManager).GetField("avatarAnimator", BindingFlags.Instance | BindingFlags.NonPublic);
                if (manager == null || field == null) throw new Exception("SDK fixture changed");
                animator = field.GetValue(manager) as Animator;
                if (animator == null) throw new Exception("Missing fixture animator");
                field.SetValue(manager, null);
            }
            else if (stage == 1) Restore();
            else { Finish(true, "Actual ClientSim bone mapping/root, disabled clicks, controlled missing-avatar cancellation and recovery checked. " + observed + "12-point bone-origin approximation, not tracked fingertips or hardware validation."); return; }
            stage++; next = Time.time + 0.5f;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void Restore()
    {
        if (manager != null && field != null && animator != null) field.SetValue(manager, animator);
        manager = null; field = null; animator = null;
    }
    private static void Finish(bool success, string text)
    {
        Restore(); SessionState.SetBool(Active, false);
        File.WriteAllText("udon-avatar-result.txt", (success ? "PASS: " : "FAIL: ") + text);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
