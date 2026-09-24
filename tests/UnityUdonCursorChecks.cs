#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;

public class UnityUdonCursorChecks : MonoBehaviour
{
    private const string Active = "Bird.Cursor.Checks";
    private static double deadline;
    private static int checks;
    public static void Run()
    {
        File.WriteAllText("udon-cursor-result.txt", "PENDING");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer) throw new Exception("ClientSim/spawnPlayer required");
        Program("BirdSphereFit"); Program("BirdCursorState");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
        for (int i = 0; i < 2; i++)
        {
            var cursor = new GameObject("Synthetic cursor " + i).AddUdonSharpComponent<BirdCursorState>();
            cursor.fitter = new GameObject("Fitter " + i).AddUdonSharpComponent<BirdSphereFit>();
            cursor.cursorVisual = new GameObject("Cursor marker " + i).transform;
            cursor.cursorVisual.gameObject.SetActive(false);
            UdonSharpEditorUtility.CopyProxyToUdon(cursor);
        }
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }
    private static void Program(string name)
    {
        string path = "Assets/BirdGenerated/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/" + name + ".cs");
        if (program.sourceCsScript == null) throw new Exception("Restore source/meta: " + name);
        AssetDatabase.CreateAsset(program, path);
        AssetDatabase.SaveAssets();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (SessionState.GetBool(Active, false)) new GameObject("Cursor VM checks").AddComponent<UnityUdonCursorChecks>();
    }
    private void Update()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("ClientSim startup timeout");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad < 2) return;
            var left = UdonSharpEditorUtility.GetBackingUdonBehaviour(GameObject.Find("Synthetic cursor 0").GetComponent<BirdCursorState>());
            var right = UdonSharpEditorUtility.GetBackingUdonBehaviour(GameObject.Find("Synthetic cursor 1").GetComponent<BirdCursorState>());
            checks = 0;
            Vector3 root = new Vector3(-0.2f, 1, 0);
            Sample(left, root, 0.02f, 0.004f);
            State(left, true, false, false, false);
            Vector3 expected = root + Vector3.forward * (0.02f + 0.02f + 0.02f * Mathf.Pow(2f / 3f, 6));
            Assert(Vector3.Distance((Vector3)left.GetProgramVariable("position"), expected) < 0.0001f, "Original range mapping");
            Sample(left, root, 0.02f, 0.008f); State(left, true, true, true, false);
            Sample(left, root, 0.02f, 0.006f); State(left, true, true, false, false);
            Sample(left, root, 0.02f, 0.004f); State(left, true, false, false, true);
            Sample(left, root, 0.02f, 0.006f); State(left, true, false, false, false);
            Sample(left, root, 0.03f, 0.008f); State(left, true, true, true, false);
            Assert(((Vector3)left.GetProgramVariable("position") - root).magnitude > (expected - root).magnitude, "Greater center distance extends cursor");
            Sample(right, new Vector3(0.2f, 1, 0), 0.02f, 0.004f); State(right, true, false, false, false);
            State(left, true, true, true, false); // right sampling cannot consume left pulses/state
            Vector3 held = (Vector3)left.GetProgramVariable("position");
            left.SetProgramVariable("tracking", false); left.SendCustomEvent("Step"); State(left, false, false, false, true);
            Assert((Vector3)left.GetProgramVariable("position") == held, "Invalid sample holds last position");
            left.SendCustomEvent("Step"); State(left, false, false, false, false);
            Sample(left, root, 0.02f, 0.008f); State(left, true, true, true, false);
            left.SetProgramVariable("points", new Vector3[4]); left.SendCustomEvent("Step"); State(left, false, false, false, true);
            Sample(left, root, 0.02f, 0.008f); State(left, true, true, true, false);
            left.SetProgramVariable("indexTip", new Vector3(float.NaN, 0, 0)); left.SendCustomEvent("Step"); State(left, false, false, false, true);
            Sample(left, root, 0.02f, 0.008f); left.SendCustomEvent("Cancel"); State(left, false, false, false, true);
            left.SendCustomEvent("Cancel"); State(left, false, false, false, false);
            Sample(left, root, 0.02f, 0.004f);
            left.SetProgramVariable("indexTip", (Vector3)left.GetProgramVariable("position") + Vector3.right * 0.022f);
            left.SendCustomEvent("Step"); State(left, true, true, true, false); // cursor-centered selection sphere
            State(right, true, false, false, false);
            Finish(true, checks + " compiled-Udon cursor assertions passed: fitter calls, range, hysteresis, both selection centers, per-sample pulses, two-instance independence, invalid/lost input, cancellation and recovery. Unsmoothed synthetic data; no avatar/hardware/render validation.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void Sample(UdonBehaviour vm, Vector3 root, float distance, float depth)
    {
        Vector3 center = root + Vector3.forward * distance;
        float r = 0.03f / Mathf.Sqrt(3);
        vm.SetProgramVariable("points", new[] { center + new Vector3(r,r,r), center + new Vector3(r,-r,-r), center + new Vector3(-r,r,-r), center + new Vector3(-r,-r,r) });
        vm.SetProgramVariable("handRoot", root);
        vm.SetProgramVariable("indexTip", center + Vector3.right * (0.03f - depth));
        vm.SetProgramVariable("tracking", true);
        vm.SendCustomEvent("Step");
    }
    private static void State(UdonBehaviour vm, bool valid, bool selected, bool down, bool up)
    {
        Assert((bool)vm.GetProgramVariable("poseValid") == valid && (bool)vm.GetProgramVariable("selected") == selected &&
            (bool)vm.GetProgramVariable("down") == down && (bool)vm.GetProgramVariable("up") == up, "Cursor state " + checks);
        var visual = (Transform)vm.GetProgramVariable("cursorVisual");
        Assert(visual.gameObject.activeSelf == valid, "Marker visibility");
        if (valid) Assert(Vector3.Distance(visual.position, (Vector3)vm.GetProgramVariable("position")) < 0.00001f, "Marker follows cursor");
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Finish(bool success, string detail)
    {
        SessionState.SetBool(Active, false);
        File.WriteAllText("udon-cursor-result.txt", (success ? "PASS: " : "FAIL: ") + detail);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
