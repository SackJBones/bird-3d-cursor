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

// Generated runtime-folder helper, excluded from client builds. Tests the compiled VM.
public class UnityUdonSphereFitChecks : MonoBehaviour
{
    private const string Active = "Bird.SphereFit.Checks";
    private static double deadline;
    private static int checks;

    public static void Run()
    {
        File.WriteAllText("udon-sphere-result.txt", "PENDING");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer)
            throw new Exception("ClientSim and spawnPlayer must already be enabled");
        string path = File.Exists("Assets/BirdWorld/Programs/BirdSphereFit.asset")
            ? "Assets/BirdWorld/Programs/BirdSphereFit.asset" : "Assets/BirdGenerated/BirdSphereFit.asset";
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if (program == null)
        {
            program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdSphereFit.cs");
            if (program.sourceCsScript == null) throw new Exception("Restore sphere source/meta first");
            AssetDatabase.CreateAsset(program, path);
            AssetDatabase.SaveAssets();
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
        new GameObject("Synthetic compiled sphere fit").AddUdonSharpComponent<BirdSphereFit>();
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (SessionState.GetBool(Active, false)) new GameObject("Sphere VM checks").AddComponent<UnityUdonSphereFitChecks>();
    }

    private void Update()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("ClientSim startup timeout");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad < 2) return;
            var proxy = FindObjectOfType<BirdSphereFit>();
            var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            if (vm == null) throw new Exception("Missing compiled sphere behaviour");
            checks = 0;
            Valid(vm, Sphere(Vector3.zero, 1, 16), Vector3.zero, 1);
            Valid(vm, Sphere(new Vector3(2, -3, 4), 0.08f, 16), new Vector3(2, -3, 4), 0.08f);
            Valid(vm, Sphere(new Vector3(2, -3, 4), 0.04f, 16), new Vector3(2, -3, 4), 0.04f);
            Valid(vm, Sphere(new Vector3(2, -3, 4), 0.12f, 16), new Vector3(2, -3, 4), 0.12f);
            Valid(vm, Sphere(new Vector3(100, -200, 300), 0.08f, 32), new Vector3(100, -200, 300), 0.08f);
            var tetra = new[] { new Vector3(1,1,1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(-1,-1,1) };
            Valid(vm, tetra, Vector3.zero, Mathf.Sqrt(3));
            // Compare a non-spherical set against Bird.cs's centered 4x4 normal equations.
            var noisy = Sphere(new Vector3(0.3f, -0.2f, 0.5f), 0.1f, 16);
            for (int i = 0; i < noisy.Length; i++) noisy[i] += new Vector3(0.003f * Mathf.Sin(i), 0.002f * Mathf.Cos(i * 2), 0);
            Vector4 reference = ReferenceFit(noisy);
            Valid(vm, noisy, new Vector3(reference.x, reference.y, reference.z), reference.w);
            Invalid(vm, null); Invalid(vm, new Vector3[0]); Invalid(vm, new Vector3[3]); Invalid(vm, new Vector3[33]);
            Invalid(vm, new[] { Vector3.one, Vector3.one, Vector3.one, Vector3.one });
            Invalid(vm, new[] { Vector3.zero, Vector3.right, Vector3.right * 2, Vector3.right * 3 });
            Invalid(vm, new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down });
            Invalid(vm, new[] { Vector3.right, Vector3.left, Vector3.up, new Vector3(0, -1, 0.00001f) });
            var invalid = Sphere(Vector3.zero, 1, 16); invalid[7].x = float.NaN; Invalid(vm, invalid);
            invalid = Sphere(Vector3.zero, 1, 16); invalid[7].y = float.PositiveInfinity; Invalid(vm, invalid);
            Valid(vm, Sphere(Vector3.one, 0.2f, 16), Vector3.one, 0.2f);
            string palmResult = UnityPalmFitChecks.Check((points, root, normal, cap, enabled) => {
                vm.SetProgramVariable("points", points); vm.SetProgramVariable("palmOrigin", root);
                vm.SetProgramVariable("palmNormal", normal); vm.SetProgramVariable("maximumCenterDistance", cap);
                vm.SetProgramVariable("constrainToPalm", enabled); vm.SendCustomEvent("Fit");
            }, () => (bool)vm.GetProgramVariable("fitValid"), () => (Vector3)vm.GetProgramVariable("center"), () => (float)vm.GetProgramVariable("radius"));
            Finish(true, checks + " compiled-Udon baseline sphere cases passed. " + palmResult + ". Synthetic points only; no avatar mapping/click/hardware validation.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    private static Vector3[] Sphere(Vector3 center, float radius, int count)
    {
        var points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float y = 1 - 2f * (i + 0.5f) / count;
            float r = Mathf.Sqrt(1 - y * y);
            float angle = i * 2.39996323f;
            points[i] = center + Quaternion.Euler(17, 31, -23) * new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle)) * radius;
        }
        return points;
    }

    private static Vector4 ReferenceFit(Vector3[] points)
    {
        Vector3 mean = Vector3.zero;
        foreach (var point in points) mean += point;
        mean /= points.Length;
        Matrix4x4 normal = new Matrix4x4();
        Vector4 rhs = Vector4.zero;
        foreach (var point in points)
        {
            Vector3 p = point - mean;
            Vector4 row = new Vector4(2 * p.x, 2 * p.y, 2 * p.z, 1);
            for (int r = 0; r < 4; r++)
            {
                rhs[r] += row[r] * p.sqrMagnitude;
                for (int c = 0; c < 4; c++) normal[r, c] += row[r] * row[c];
            }
        }
        Vector4 fit = normal.inverse * rhs;
        Vector3 offset = new Vector3(fit.x, fit.y, fit.z);
        Vector3 center = mean + offset;
        return new Vector4(center.x, center.y, center.z, Mathf.Sqrt(fit.w + offset.sqrMagnitude));
    }

    private static void Valid(UdonBehaviour vm, Vector3[] points, Vector3 center, float radius)
    {
        vm.SetProgramVariable("points", points);
        vm.SendCustomEvent("Fit");
        var actual = (Vector3)vm.GetProgramVariable("center");
        float actualRadius = (float)vm.GetProgramVariable("radius");
        if (!(bool)vm.GetProgramVariable("fitValid") || !Finite(actual.x) || !Finite(actual.y) || !Finite(actual.z) || !Finite(actualRadius) ||
            Vector3.Distance(actual, center) > 0.0002f || Mathf.Abs(actualRadius - radius) > 0.0002f)
            throw new Exception("Analytic sphere mismatch case " + checks + " center=" + actual + " radius=" + actualRadius);
        checks++;
    }

    private static void Invalid(UdonBehaviour vm, Vector3[] points)
    {
        vm.SetProgramVariable("points", points);
        vm.SendCustomEvent("Fit");
        if ((bool)vm.GetProgramVariable("fitValid") || (Vector3)vm.GetProgramVariable("center") != Vector3.zero || (float)vm.GetProgramVariable("radius") != 0)
            throw new Exception("Invalid set retained valid/stale output case " + checks);
        checks++;
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Finish(bool success, string text)
    {
        SessionState.SetBool(Active, false);
        File.WriteAllText("udon-sphere-result.txt", (success ? "PASS: " : "FAIL: ") + text);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
