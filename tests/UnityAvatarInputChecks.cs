#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Globalization;
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
    private static float originalHeight;
    private static float[] baselineSpans = new float[2];
    private static float[] baselineDistances = new float[2];
    private static float[] baselineRadii = new float[2];
    private static bool Calibration { get { return SessionState.GetBool("Bird.AvatarInput.Calibration", false); } }
    private static bool Neutral { get { return SessionState.GetBool("Bird.AvatarInput.Neutral", false); } }
    private static bool PoseVariation { get { return SessionState.GetBool("Bird.AvatarInput.Pose", false); } }
    private static string ResultPath { get { return PoseVariation ? "udon-avatar-pose-result.txt" : Neutral ? "udon-avatar-neutral-result.txt" : Calibration ? "udon-avatar-calibration-result.txt" : "udon-avatar-result.txt"; } }
    public static void Run()
    {
        Begin(false);
    }
    public static void RunScaleCalibration() { Begin(true); }
    public static void RunNeutralPreview() { Begin(false, true); }
    public static void RunPoseVariation() { Begin(false, false, true); }
    private static void Begin(bool calibration, bool neutral = false, bool pose = false)
    {
        SessionState.SetBool("Bird.AvatarInput.Calibration", calibration);
        SessionState.SetBool("Bird.AvatarInput.Neutral", neutral);
        SessionState.SetBool("Bird.AvatarInput.Pose", pose);
        File.WriteAllText(ResultPath, "PENDING");
        if (calibration) File.WriteAllText("udon-avatar-calibration.csv", "hand,scale,eye_height_m,span_m,fit_radius_m,center_distance_m,distance_over_span,rms_residual_over_span,raw_range_m,baseline_normalized_range_m\n");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer) throw new Exception("ClientSim required");
        string path = File.Exists("Assets/BirdWorld/Programs/BirdAvatarInput.asset")
            ? "Assets/BirdWorld/Programs/BirdAvatarInput.asset" : "Assets/BirdGenerated/BirdAvatarInput.asset";
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
            if (PoseVariation)
            {
                if (UnityAvatarPoseFixture.Tick(inputs)) Finish(true, "Compiled adapter consumed controlled +/-15 degree local-Z finger bends; chain lengths preserved, fit/range response recorded and neutral target recovered. CSV in Validation/AvatarPose. This is synthetic articulation, not physical gesture fidelity.");
                return;
            }
            if (Neutral) { CheckNeutral(inputs); return; }
            foreach (var proxy in inputs)
            {
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                var cursor = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy.cursor);
                bool ready = (bool)vm.GetProgramVariable("dataReady");
                if ((bool)cursor.GetProgramVariable("clicksAllowed") || (bool)cursor.GetProgramVariable("selected") || (bool)cursor.GetProgramVariable("down")) throw new Exception("Avatar approximation enabled clicks");
                if (!Calibration && stage == 1)
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
                if (Calibration) RecordCalibration(proxy, points, root, measured);
            }
            if (Calibration)
            {
                if (stage == 0) originalHeight = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();
                if (originalHeight < 0.2f || originalHeight > 60 || float.IsNaN(originalHeight)) throw new Exception("Baseline height outside unclamped scale test");
                if (stage == 3) { Finish(true, "Both hands at 1x, 0.5x, 1.5x and restored scale: normalized hand geometry and baseline-normalized range checked. Metrics in udon-avatar-calibration.csv. Baseline normalization is a diagnostic, not a calibrated control mapping."); return; }
                stage++;
                Networking.LocalPlayer.SetAvatarEyeHeightByMeters(originalHeight * (stage == 1 ? 0.5f : stage == 2 ? 1.5f : 1f));
                next = Time.time + 0.5f;
                return;
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
    private static void CheckNeutral(BirdAvatarInput[] inputs)
    {
        foreach (var proxy in inputs)
        {
            var input = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            var cursor = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy.cursor);
            bool calibrated = (bool)input.GetProgramVariable("calibrated");
            if ((bool)cursor.GetProgramVariable("clicksAllowed") || (bool)cursor.GetProgramVariable("selected")) throw new Exception("Calibrated adapter enabled clicks");
            if (stage == 4)
            {
                if (calibrated || (bool)input.GetProgramVariable("dataReady") || (bool)cursor.GetProgramVariable("poseValid")) throw new Exception("Loss retained calibration");
                continue;
            }
            if (!(bool)input.GetProgramVariable("dataReady")) throw new Exception("Neutral preview missing data");
            if (stage == 0 || stage == 5)
            {
                if (calibrated) throw new Exception("Unexpected automatic calibration");
                if (stage == 5)
                {
                    input.SetProgramVariable("neutralPreviewRange", float.NaN);
                    input.SendCustomEvent("CalibrateNeutral");
                    if ((bool)input.GetProgramVariable("calibrated")) throw new Exception("Invalid target accepted");
                    input.SetProgramVariable("neutralPreviewRange", 0.3f);
                }
                input.SendCustomEvent("CalibrateNeutral");
                if (!(bool)input.GetProgramVariable("calibrated")) throw new Exception("Neutral calibration rejected valid data");
            }
            else
            {
                Vector3 root = (Vector3)cursor.GetProgramVariable("handRoot");
                float rawRange = Vector3.Distance(root, (Vector3)cursor.GetProgramVariable("rawPosition"));
                if (!calibrated || !(bool)cursor.GetProgramVariable("poseValid") || !Finite(rawRange) || Mathf.Abs(rawRange - 0.3f) > 0.002f)
                    throw new Exception("Neutral range not preserved: " + rawRange + " at stage " + stage);
                if (stage == 6)
                {
                    input.SendCustomEvent("ResetCalibration");
                    if ((bool)input.GetProgramVariable("calibrated") || (bool)cursor.GetProgramVariable("poseValid")) throw new Exception("Reset did not invalidate calibration/cursor");
                }
            }
        }
        if (stage == 0) originalHeight = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();
        if (stage == 1) Networking.LocalPlayer.SetAvatarEyeHeightByMeters(originalHeight * 0.5f);
        if (stage == 2) Networking.LocalPlayer.SetAvatarEyeHeightByMeters(originalHeight * 1.5f);
        if (stage == 3)
        {
            manager = Networking.LocalPlayer.GetClientSimPlayer().GetAvatarDataProvider() as ClientSimPlayerAvatarManager;
            field = typeof(ClientSimPlayerAvatarManager).GetField("avatarAnimator", BindingFlags.Instance | BindingFlags.NonPublic);
            if (manager == null || field == null) throw new Exception("SDK fixture changed");
            animator = field.GetValue(manager) as Animator;
            if (animator == null) throw new Exception("Missing animator");
            field.SetValue(manager, null);
        }
        if (stage == 4) Restore();
        if (stage == 6) { Finish(true, "Both neutral previews anchored at 0.3 m within 2 mm across 1x/0.5x/1.5x avatar size; loss invalidated calibration, NaN target rejected, explicit recalibration and reset checked. Clicks disabled. One simulator pose; no avatar-swap/physical fidelity validation."); return; }
        stage++; next = Time.time + 0.6f;
    }
    private static void RecordCalibration(BirdAvatarInput proxy, Vector3[] points, Vector3 root, float rawRange)
    {
        var fit = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy.cursor.fitter);
        if (!(bool)fit.GetProgramVariable("fitValid")) throw new Exception("Calibration fixture requires accepted sphere fit");
        Vector3 center = (Vector3)fit.GetProgramVariable("center");
        float radius = (float)fit.GetProgramVariable("radius");
        Vector4 reference = ReferenceFit(points);
        if (Vector3.Distance(center, new Vector3(reference.x, reference.y, reference.z)) > 0.0002f || Mathf.Abs(radius - reference.w) > 0.0002f)
            throw new Exception("Avatar fit differs from original centered 4x4 equations");
        float span = Vector3.Distance(points[3], points[5]); // middle proximal to distal, not tip
        float distance = Vector3.Distance(center, root);
        if (span <= 0.001f || !Finite(span) || !Finite(distance) || !Finite(radius)) throw new Exception("Unusable metric");
        int hand = proxy.rightHand ? 1 : 0;
        if (stage == 0) { baselineSpans[hand] = span; baselineDistances[hand] = distance; baselineRadii[hand] = radius; }
        float factor = stage == 1 ? 0.5f : stage == 2 ? 1.5f : 1f;
        if (Mathf.Abs(span / baselineSpans[hand] - factor) > 0.002f ||
            Mathf.Abs(distance / baselineDistances[hand] - factor) > 0.002f ||
            Mathf.Abs(radius / baselineRadii[hand] - factor) > 0.002f) throw new Exception("Geometry did not scale proportionally");
        float normalizedDistance = distance * baselineSpans[hand] / span;
        float normalizedRange = Range(normalizedDistance);
        float baselineRange = Range(baselineDistances[hand]);
        if (Mathf.Abs(normalizedRange / baselineRange - 1) > 0.01f) throw new Exception("Baseline normalization did not stabilize range");
        if (Mathf.Abs(rawRange / Range(distance) - 1) > 0.01f) throw new Exception("Observed range differs from original range law");
        float residual = 0;
        foreach (var p in points) { float error = Vector3.Distance(p, center) - radius; residual += error * error; }
        residual = Mathf.Sqrt(residual / points.Length) / span;
        float[] metrics = { factor, Networking.LocalPlayer.GetAvatarEyeHeightAsMeters(), span, radius, distance, distance / span, residual, rawRange, normalizedRange };
        string row = proxy.rightHand ? "right" : "left";
        foreach (float value in metrics) { if (!Finite(value)) throw new Exception("Nonfinite metric"); row += "," + value.ToString("R", CultureInfo.InvariantCulture); }
        File.AppendAllText("udon-avatar-calibration.csv", row + "\n");
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static Vector4 ReferenceFit(Vector3[] points)
    {
        // Independent ordinary C# form of Bird.cs's original centered normal equations.
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
        Vector4 solution = normal.inverse * rhs;
        Vector3 offset = new Vector3(solution.x, solution.y, solution.z);
        Vector3 center = mean + offset;
        float radius = Mathf.Sqrt(solution.w + offset.sqrMagnitude);
        if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z) || !Finite(radius)) throw new Exception("Nonfinite reference fit");
        return new Vector4(center.x, center.y, center.z, radius);
    }
    private static float Range(float distance) { return distance + distance * distance / 0.02f + 0.02f * Mathf.Pow(distance / 0.03f, 6); }
    private static void Finish(bool success, string text)
    {
        UnityAvatarPoseFixture.Restore();
        Restore(); SessionState.SetBool(Active, false);
        if (originalHeight > 0 && Utilities.IsValid(Networking.LocalPlayer)) Networking.LocalPlayer.SetAvatarEyeHeightByMeters(originalHeight);
        File.WriteAllText(ResultPath, (success ? "PASS: " : "FAIL: ") + text);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
