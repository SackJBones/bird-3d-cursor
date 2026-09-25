#if UDON
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Editor;
using VRC.SDK3.Components;

// Editor-folder helper. Uses the public SDK build-only API, never upload or client launch.
public static class UnityWorldBundleChecks
{
    private static double deadline;
    private static bool done;
    private static VRCSdkControlPanel panel;
    private static VRCSceneDescriptor descriptor;
    private const string Pref = "VRC.SDKBase_StripAllShaders";
    private static bool hadPref, oldPref;

    public static async void Run()
    {
        File.WriteAllText("world-bundle-result.txt", "PENDING");
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += Timeout;
        hadPref = EditorPrefs.HasKey(Pref); oldPref = EditorPrefs.GetBool(Pref);
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new Exception("This check requires the Windows x64 build target");
            var scene = EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdAvatarPreview.unity");
            descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            if (descriptor == null) throw new Exception("Missing world descriptor");
            if (descriptor.GetComponent<PipelineManager>() == null) descriptor.gameObject.AddComponent<PipelineManager>();
            // SDK preprocessing may save or modify the active scene; isolate that work.
            if (!EditorSceneManager.SaveScene(scene, "Assets/BirdGenerated/AvatarBuildValidation.unity")) throw new Exception("Could not save build fixture");
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile error");
            panel = ScriptableObject.CreateInstance<VRCSdkControlPanel>();
            // The SDK registers this builder for the normal Worlds pipeline. The
            // similarly named V3 subclass is gated by the legacy V3SdkUI switch.
            var builder = new VRCSdkControlPanelWorldBuilder();
            builder.RegisterBuilder(panel);
            if (!builder.IsValidBuilder(out string message)) throw new Exception("SDK builder invalid: " + message);
            string path = await ((IVRCSdkWorldBuilderApi)builder).Build();
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || new FileInfo(path).Length == 0) throw new Exception("SDK returned no bundle");
            Directory.CreateDirectory("../Validation/WorldBuild");
            string output = "../Validation/WorldBuild/BirdAvatarPreview.vrcw";
            File.Copy(path, output, true);
            string hash;
            using (var stream = File.OpenRead(output)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            Finish(true, "SDK build-only Windows world bundle: " + new FileInfo(output).Length + " bytes; SHA256=" + hash + ". No client launch, upload, multiplayer or headset validation.");
        }
        catch (Exception e)
        {
            string detail = e.ToString();
            if (panel != null)
            {
                try
                {
                    detail += "\nScene issues: " + string.Join("\n", panel.GetGuiErrorsOrIssuesForItem(descriptor).Select(i => i.issueText));
                    detail += "\nProject issues: " + string.Join("\n", panel.GetGuiErrorsOrIssuesForItem(panel).Select(i => i.issueText));
                }
                catch (Exception diagnostic) { detail += "\nIssue listing unavailable: " + diagnostic.Message; }
            }
            Finish(false, detail);
        }
    }
    private static void Timeout()
    {
        if (!done && EditorApplication.timeSinceStartup > deadline) Finish(false, "SDK build exceeded four-minute editor watchdog");
    }
    private static void Finish(bool success, string text)
    {
        if (done) return;
        done = true; EditorApplication.update -= Timeout;
        if (hadPref) EditorPrefs.SetBool(Pref, oldPref); else EditorPrefs.DeleteKey(Pref);
        File.WriteAllText("world-bundle-result.txt", (success ? "PASS: " : "FAIL: ") + text);
        if (panel != null) UnityEngine.Object.DestroyImmediate(panel);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif

public static class UnityWorldSdkSetup
{
    // Explicit project setup, separate from the build validation entry point.
    public static void RunLayers()
    {
        if (!UpdateLayers.AreLayersSetup()) UpdateLayers.SetupEditorLayers();
        if (!UpdateLayers.IsCollisionLayerMatrixSetup()) UpdateLayers.SetupCollisionLayerMatrix();
        UnityEditor.AssetDatabase.SaveAssets();
        bool valid = UpdateLayers.AreLayersSetup() && UpdateLayers.IsCollisionLayerMatrixSetup();
        System.IO.File.WriteAllText("world-layer-setup-result.txt", valid ? "PASS: SDK layers and collision matrix configured" : "FAIL: SDK layer setup did not validate");
        UnityEditor.EditorApplication.Exit(valid ? 0 : 1);
    }

    public static void Run()
    {
        VRC.Editor.EnvConfig.SetActiveSDKDefines();
        UnityEditor.AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("world-sdk-setup-result.txt", "PASS: SDK active-platform define setup invoked; restart editor before build.");
        UnityEditor.EditorApplication.Exit(0);
    }
}
