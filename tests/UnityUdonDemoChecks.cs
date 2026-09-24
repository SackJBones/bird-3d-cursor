#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;

public class UnityUdonDemoChecks : MonoBehaviour
{
    private const string ScenePath = "Assets/BirdWorld/Scenes/BirdSyntheticDemo.unity";
    private const string Active = "Bird.Demo.Checks";
    private static double deadline;
    private static int stage;
    private static float resumeAt;
    public static void AddControls()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (FindObjectsOfType<BirdDemoControl>().Length != 0) throw new Exception("Controls already exist; refusing duplicate upgrade");
        const string path = "Assets/BirdWorld/Programs/BirdDemoControl.asset";
        var source = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdDemoControl.cs");
        if (source == null) throw new Exception("Restore control source/meta first");
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if (program == null)
        {
            program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = source;
            AssetDatabase.CreateAsset(program, path);
        }
        else if (program.sourceCsScript != source) throw new Exception("Existing control program references another source");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
        var left = GameObject.Find("Left synthetic").GetComponent<BirdSyntheticDemo>();
        var right = GameObject.Find("Right synthetic").GetComponent<BirdSyntheticDemo>();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/Materials/SyntheticControl.mat");
        if (material == null) material = Material("Control", new Color(0.08f, 0.15f, 0.2f));
        for (int i = 0; i < 2; i++)
        {
            string name = i == 0 ? "Pause control" : "Clear control";
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.position = new Vector3(i == 0 ? -0.27f : 0.27f, 0.57f, 0.1f);
            button.transform.localScale = new Vector3(0.45f, 0.16f, 0.08f);
            button.GetComponent<Renderer>().sharedMaterial = material;
            var control = button.AddUdonSharpComponent<BirdDemoControl>();
            control.left = left; control.right = right; control.clearOnly = i == 1;
            control.label = Label(name + " label", button.transform.position - Vector3.forward * 0.045f, 220, 65);
            control.label.text = i == 0 ? "PAUSE" : "CLEAR TRAILS";
            UdonSharpEditorUtility.CopyProxyToUdon(control);
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(control);
            backing.interactText = i == 0 ? "Pause / resume demo" : "Clear trails";
            backing.proximity = 3;
        }
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Scene save failed");
        AssetDatabase.SaveAssets();
        File.WriteAllText("udon-demo-controls-result.txt", "PASS: local interaction controls saved; runtime check separate.");
        EditorApplication.Exit(0);
    }
    public static void Generate()
    {
        if (File.Exists(ScenePath)) throw new Exception("Refusing to overwrite demo scene");
        if (!AssetDatabase.IsValidFolder("Assets/BirdWorld/Materials")) AssetDatabase.CreateFolder("Assets/BirdWorld", "Materials");
        foreach (string name in new[] { "BirdSphereFit", "BirdCursorState", "BirdSyntheticDemo" })
        {
            string path = "Assets/BirdWorld/Programs/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) throw new Exception("Program already exists: " + path);
            // Remove only prior generated validation programs for these same sources.
            AssetDatabase.DeleteAsset("Assets/BirdGenerated/" + name + ".asset");
            var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/" + name + ".cs");
            if (program.sourceCsScript == null) throw new Exception("Restore source/meta " + name);
            AssetDatabase.CreateAsset(program, path);
        }
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
        var scene = EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
        var landmark = GameObject.Find("Orientation landmark - Bird hand probe pending");
        if (landmark != null) DestroyImmediate(landmark);
        Text title = Label("Title", new Vector3(0, 2.15f, 1), 720, 150);
        title.text = "BIRD / SYNTHETIC MOTION\nTwo cursors, trails and click feedback\nDemo input only - no hand tracking";
        Material pressed = Material("Pressed", new Color(1, 0.85f, 0.25f));
        for (int i = 0; i < 2; i++)
        {
            var demo = new GameObject(i == 0 ? "Left synthetic" : "Right synthetic").AddUdonSharpComponent<BirdSyntheticDemo>();
            demo.transform.position = new Vector3(i == 0 ? -0.45f : 0.45f, 1.1f, 0.3f);
            demo.phase = i * 1.7f;
            demo.handLabel = i == 0 ? "LEFT" : "RIGHT";
            demo.cursor = new GameObject(demo.handLabel + " cursor state").AddUdonSharpComponent<BirdCursorState>();
            demo.cursor.fitter = new GameObject(demo.handLabel + " sphere fitter").AddUdonSharpComponent<BirdSphereFit>();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = demo.handLabel + " cursor";
            DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.localScale = Vector3.one * 0.045f;
            demo.cursor.cursorVisual = sphere.transform;
            demo.marker = sphere.GetComponent<Renderer>();
            demo.idleMaterial = Material(demo.handLabel, i == 0 ? new Color(0.1f, 0.85f, 1) : new Color(1, 0.2f, 0.7f));
            demo.pressedMaterial = pressed;
            demo.marker.sharedMaterial = demo.idleMaterial;
            sphere.SetActive(false);
            demo.trail = new GameObject(demo.handLabel + " bounded trail").AddComponent<LineRenderer>();
            demo.trail.sharedMaterial = demo.idleMaterial;
            demo.trail.useWorldSpace = true;
            demo.trail.widthMultiplier = 0.012f;
            demo.trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            demo.trail.positionCount = 0;
            demo.label = Label(demo.handLabel + " label", new Vector3(i == 0 ? -0.55f : 0.55f, 0.85f, 0.6f), 300, 100);
            demo.label.text = demo.handLabel + " / SYNTHETIC\nStarting";
            UdonSharpEditorUtility.CopyProxyToUdon(demo.cursor);
            UdonSharpEditorUtility.CopyProxyToUdon(demo);
        }
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Scene save failed");
        AssetDatabase.SaveAssets();
        File.WriteAllText("udon-demo-generate-result.txt", "PASS: synthetic scene/programs/materials saved; runtime check is separate.");
        EditorApplication.Exit(0);
    }
    private static Material Material(string name, Color color)
    {
        var material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        AssetDatabase.CreateAsset(material, "Assets/BirdWorld/Materials/Synthetic" + name + ".mat");
        return material;
    }
    private static Text Label(string name, Vector3 position, float width, float height)
    {
        var canvas = new GameObject(name, typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.position = position;
        canvas.transform.localScale = Vector3.one * 0.002f;
        var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(canvas.transform, false);
        label.rectTransform.sizeDelta = new Vector2(width, height);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        return label;
    }
    public static void Validate()
    {
        File.WriteAllText("udon-demo-result.txt", "PENDING");
        if (!ClientSimSettings.Instance.enableClientSim || !ClientSimSettings.Instance.spawnPlayer) throw new Exception("ClientSim required");
        EditorSceneManager.OpenScene(ScenePath);
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (SessionState.GetBool(Active, false)) new GameObject("Demo checks").AddComponent<UnityUdonDemoChecks>();
    }
    private void Update()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Demo timed out");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad < 5 || Time.time < resumeAt) return;
            var demos = FindObjectsOfType<BirdSyntheticDemo>();
            if (demos.Length != 2) throw new Exception("Expected two demo instances");
            var pauseProxy = GameObject.Find("Pause control").GetComponent<BirdDemoControl>();
            var clearProxy = GameObject.Find("Clear control").GetComponent<BirdDemoControl>();
            var pause = UdonSharpEditorUtility.GetBackingUdonBehaviour(pauseProxy);
            var clear = UdonSharpEditorUtility.GetBackingUdonBehaviour(clearProxy);
            if (!pauseProxy.GetComponent<Collider>().enabled || !clearProxy.GetComponent<Collider>().enabled)
                throw new Exception("Interaction collider disabled");
            foreach (var proxy in demos)
            {
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                var trail = (LineRenderer)vm.GetProgramVariable("trail");
                var label = (Text)vm.GetProgramVariable("label");
                if (stage == 1)
                {
                    if (trail.positionCount != 0 || !label.text.Contains("Paused")) throw new Exception("Pause did not clear trail/label");
                }
                else
                {
                    if ((int)vm.GetProgramVariable("clicks") < 1 || trail.positionCount < 2 || trail.positionCount > 64 || !label.text.Contains("SYNTHETIC"))
                        throw new Exception("Missing click, bounded trail or synthetic label");
                }
            }
            if (stage < 2)
            {
                if (!pause.RunEvent("_interact")) throw new Exception("Pause/resume Interact event missing");
                if (((Text)pause.GetProgramVariable("label")).text != (stage == 0 ? "RESUME" : "PAUSE")) throw new Exception("Control label mismatch");
                stage++; resumeAt = Time.time + (stage == 2 ? 2.2f : 0.5f); return;
            }
            if (stage == 2)
            {
                int leftClicks = (int)UdonSharpEditorUtility.GetBackingUdonBehaviour(demos[0]).GetProgramVariable("clicks");
                if (!clear.RunEvent("_interact")) throw new Exception("Clear Interact event missing");
                foreach (var proxy in demos)
                {
                    var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                    if (((LineRenderer)vm.GetProgramVariable("trail")).positionCount != 0) throw new Exception("Clear left trail points");
                }
                if ((int)UdonSharpEditorUtility.GetBackingUdonBehaviour(demos[0]).GetProgramVariable("clicks") != leftClicks) throw new Exception("Clear reset click count");
                stage++; resumeAt = Time.time + 2.2f; return;
            }
            // Isolate authored demo visuals from the simulator avatar and introductory UI.
            // These runtime layer changes are restored after capture and never saved.
            var layers = new System.Collections.Generic.Dictionary<Transform, int>();
            foreach (string name in new[] { "Feasibility floor", "Title", "LEFT cursor", "RIGHT cursor", "LEFT bounded trail", "RIGHT bounded trail", "LEFT label", "RIGHT label", "Pause control", "Clear control", "Pause control label", "Clear control label" })
                foreach (Transform item in GameObject.Find(name).GetComponentsInChildren<Transform>(true))
                { layers[item] = item.gameObject.layer; item.gameObject.layer = 30; }
            var camera = new GameObject("Validation capture").AddComponent<Camera>();
            camera.cullingMask = 1 << 30;
            camera.transform.position = new Vector3(0, 1.5f, -2.5f);
            camera.transform.LookAt(new Vector3(0, 1.5f, 0.6f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
            var target = new RenderTexture(1200, 800, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
            Directory.CreateDirectory("../Validation/UdonDemo");
            File.WriteAllBytes("../Validation/UdonDemo/synthetic.png", image.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null;
            Destroy(image); target.Release(); Destroy(target);
            foreach (var item in layers) item.Key.gameObject.layer = item.Value;
            Finish(true, "Saved scene reopened; compiled Interact handlers paused/resumed both demos and cleared trails without resetting clicks; trails rebuilt. Labels/colliders checked and camera capture saved. Event dispatch is not a physical pointer/controller test. Synthetic input only.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void Finish(bool success, string text)
    {
        SessionState.SetBool(Active, false);
        File.WriteAllText("udon-demo-result.txt", (success ? "PASS: " : "FAIL: ") + text);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
