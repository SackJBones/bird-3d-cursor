#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;

public class UnityAvatarSceneChecks : MonoBehaviour
{
    private const string ScenePath = "Assets/BirdWorld/Scenes/BirdAvatarPreview.unity";
    private const string Active = "Bird.AvatarScene.Checks";
    private static double deadline;
    private static float next;
    private static int stage;
    public static void Generate()
    {
        if (File.Exists(ScenePath)) throw new Exception("Refusing to overwrite avatar preview scene");
        foreach (string name in new[] { "BirdAvatarInput", "BirdAvatarControl" })
        {
            string path = "Assets/BirdWorld/Programs/" + name + ".asset";
            var source = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/" + name + ".cs");
            if (source == null) throw new Exception("Restore source/meta " + name);
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            string generated = "Assets/BirdGenerated/" + name + ".asset";
            if (program == null && File.Exists(generated))
            {
                string error = AssetDatabase.MoveAsset(generated, path);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
                program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            }
            if (program == null)
            {
                program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript = source;
                AssetDatabase.CreateAsset(program, path);
            }
            else if (program.sourceCsScript != source) throw new Exception("Existing program source mismatch");
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Compile();
        var scene = EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
        var landmark = GameObject.Find("Orientation landmark - Bird hand probe pending");
        if (landmark != null) DestroyImmediate(landmark);
        var visuals = new GameObject("Avatar preview visuals");
        Text title = Label("Title", new Vector3(0, 2.2f, -0.8f), 1050, 180, visuals.transform);
        title.text = "BIRD / EXPERIMENTAL AVATAR PREVIEW\nHold a comfortable hand pose, then CALIBRATE\nAvatar bone approximation - clicks disabled";
        Text feedback = Label("Feedback", new Vector3(0, 1.9f, -0.8f), 1050, 100, visuals.transform);
        feedback.fontSize = 22;
        feedback.text = "Preview starts hidden. RESET clears calibration.";
        var inputs = new BirdAvatarInput[2];
        for (int i = 0; i < 2; i++)
        {
            string hand = i == 0 ? "Left" : "Right";
            var input = new GameObject(hand + " avatar input").AddUdonSharpComponent<BirdAvatarInput>();
            inputs[i] = input; input.rightHand = i == 1; input.requireCalibration = true;
            input.cursor = new GameObject(hand + " avatar cursor").AddUdonSharpComponent<BirdCursorState>();
            input.cursor.fitter = new GameObject(hand + " avatar fitter").AddUdonSharpComponent<BirdSphereFit>();
            input.cursor.smoothing = true; input.cursor.clicksAllowed = false;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = hand + " avatar marker"; marker.transform.SetParent(visuals.transform);
            DestroyImmediate(marker.GetComponent<Collider>()); marker.transform.localScale = Vector3.one * 0.045f;
            marker.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/Materials/Synthetic" + hand.ToUpperInvariant() + ".mat");
            marker.SetActive(false); input.cursor.cursorVisual = marker.transform;
            input.label = Label(hand + " status", new Vector3(i == 0 ? -0.65f : 0.65f, 1.05f, -0.8f), 600, 150, visuals.transform);
            input.label.fontSize = 23; input.label.text = hand + " / Waiting for local avatar";
            UdonSharpEditorUtility.CopyProxyToUdon(input.cursor); UdonSharpEditorUtility.CopyProxyToUdon(input);
        }
        for (int i = 0; i < 2; i++)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = i == 0 ? "Calibrate avatar" : "Reset avatar"; button.transform.SetParent(visuals.transform);
            button.transform.position = new Vector3(i == 0 ? -0.34f : 0.34f, 0.7f, -1.1f);
            button.transform.localScale = new Vector3(0.55f, 0.16f, 0.08f);
            button.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/Materials/SyntheticControl.mat");
            var control = button.AddUdonSharpComponent<BirdAvatarControl>();
            control.left = inputs[0]; control.right = inputs[1]; control.resetOnly = i == 1; control.feedback = feedback;
            Label(button.name + " label", button.transform.position - Vector3.forward * 0.045f, 260, 65, visuals.transform).text = i == 0 ? "CALIBRATE" : "RESET";
            UdonSharpEditorUtility.CopyProxyToUdon(control);
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(control); backing.proximity = 3; backing.interactText = i == 0 ? "Calibrate neutral pose" : "Reset preview";
        }
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Scene save failed");
        AssetDatabase.SaveAssets();
        File.WriteAllText("avatar-scene-generate-result.txt", "PASS: experimental scene saved; runtime validation separate.");
        EditorApplication.Exit(0);
    }
    private static Text Label(string name, Vector3 position, float width, float height, Transform parent)
    {
        var canvas = new GameObject(name, typeof(RectTransform)).AddComponent<Canvas>();
        canvas.transform.SetParent(parent); canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.position = position; canvas.transform.localScale = Vector3.one * 0.002f;
        var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(canvas.transform, false); label.rectTransform.sizeDelta = new Vector2(width, height);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 26; label.alignment = TextAnchor.MiddleCenter;
        return label;
    }
    private static void Compile()
    {
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
    }
    public static void Validate()
    {
        File.WriteAllText("avatar-scene-result.txt", "PENDING");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer) throw new Exception("ClientSim required");
        EditorSceneManager.OpenScene(ScenePath); Compile(); SessionState.SetBool(Active, true); EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (SessionState.GetBool(Active, false)) new GameObject("Avatar scene checks").AddComponent<UnityAvatarSceneChecks>();
    }
    private void Update()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Avatar scene timeout");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad < 3 || Time.time < next) return;
            var inputs = FindObjectsOfType<BirdAvatarInput>();
            if (inputs.Length != 2) throw new Exception("Expected two inputs");
            foreach (var input in inputs)
            {
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(input);
                var cursor = UdonSharpEditorUtility.GetBackingUdonBehaviour(input.cursor);
                bool active = stage == 1 || stage == 3;
                if (!(bool)vm.GetProgramVariable("dataReady") || (bool)vm.GetProgramVariable("calibrated") != active || (bool)cursor.GetProgramVariable("poseValid") != active)
                    throw new Exception("Calibration gate mismatch stage " + stage);
                if ((bool)cursor.GetProgramVariable("clicksAllowed") || (bool)cursor.GetProgramVariable("selected")) throw new Exception("Preview enabled clicks");
                var marker = (Transform)cursor.GetProgramVariable("cursorVisual");
                if (marker.gameObject.activeSelf != active || marker.GetComponent<Collider>() != null) throw new Exception("Marker gate/collider mismatch");
                if (active && Mathf.Abs(Vector3.Distance((Vector3)cursor.GetProgramVariable("rawPosition"), (Vector3)cursor.GetProgramVariable("handRoot")) - 0.3f) > 0.002f)
                    throw new Exception("Preview neutral range mismatch");
            }
            if (stage < 3)
            {
                var button = GameObject.Find(stage == 1 ? "Reset avatar" : "Calibrate avatar");
                if (!button.GetComponent<Collider>().enabled) throw new Exception("Control collider unavailable");
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(button.GetComponent<BirdAvatarControl>());
                if (!vm.RunEvent("_interact")) throw new Exception("Missing Interact event");
                var feedback = (Text)vm.GetProgramVariable("feedback");
                if (!feedback.text.Contains(stage == 1 ? "reset" : "Both hands calibrated")) throw new Exception("Control feedback mismatch");
                stage++; next = Time.time + 0.6f; return;
            }
            Capture();
            Finish(true, "Saved preview reopened: hidden-before-calibration, Interact calibration, 0.3 m targets, reset/hidden hold and recalibration passed for both hands; clicks disabled. Capture saved. No physical pointer/headset validation.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void Capture()
    {
        var layers = new System.Collections.Generic.Dictionary<Transform, int>();
        foreach (Transform item in GameObject.Find("Avatar preview visuals").GetComponentsInChildren<Transform>(true)) { layers[item] = item.gameObject.layer; item.gameObject.layer = 30; }
        var camera = new GameObject("Preview capture").AddComponent<Camera>(); camera.cullingMask = 1 << 30;
        camera.transform.position = new Vector3(0, 1.5f, -4.5f); camera.transform.LookAt(new Vector3(0, 1.5f, -1.5f));
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
        var target = new RenderTexture(1200, 800, 24); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1200, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
        Directory.CreateDirectory("../Validation/AvatarPreview"); File.WriteAllBytes("../Validation/AvatarPreview/preview.png", image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(image);
        foreach (var item in layers) item.Key.gameObject.layer = item.Value;
    }
    private static void Finish(bool success, string text)
    {
        SessionState.SetBool(Active, false); File.WriteAllText("avatar-scene-result.txt", (success ? "PASS: " : "FAIL: ") + text); EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
