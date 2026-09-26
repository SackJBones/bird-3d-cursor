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

    public static void Run() { BuildScene("BirdAvatarPreview",BuildTarget.StandaloneWindows64); }
    public static void RunUi() { BuildScene("BirdUiDemo",BuildTarget.StandaloneWindows64); }
    public static void RunUiAndroid() { BuildScene("BirdUiDemo",BuildTarget.Android); }
    public static void AuditUiBundles()
    {
        try
        {
            string result="PASS: Unity loaded both generated UI bundle catalogs.";
            foreach(string name in new[]{"BirdUiDemo","BirdUiDemo_Android"})
            {
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/WorldBuild/"+name+".vrcw"));
                if(!File.Exists(path)) throw new Exception("Missing bundle "+path);
                // GetCRCForAssetBundle reads a .manifest sidecar, which the SDK
                // output copy does not retain. Read the actual bundle instead.
                var bundle=AssetBundle.LoadFromFile(path);
                if(bundle==null) throw new Exception("Unity could not read bundle catalog: "+path);
                string[] scenes=bundle.GetAllScenePaths();
                bundle.Unload(true);
                if(scenes.Length!=1 || !scenes[0].EndsWith("BirdUiDemoBuildValidation.unity",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected scene catalog: "+string.Join(",",scenes));
                result+="\n"+name+": "+new FileInfo(path).Length+" bytes, scene="+scenes[0];
            }
            File.WriteAllText("world-ui-bundle-audit-result.txt",result+"\nCatalog loading is not scene instantiation, client loading or runtime validation.");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("world-ui-bundle-audit-result.txt","FAIL: "+e); EditorApplication.Exit(1); }
    }
    private static async void BuildScene(string sceneName,BuildTarget target)
    {
        File.WriteAllText("world-bundle-result.txt", "PENDING");
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += Timeout;
        hadPref = EditorPrefs.HasKey(Pref); oldPref = EditorPrefs.GetBool(Pref);
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new Exception("This check requires build target " + target);
            var scene = EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/" + sceneName + ".unity");
            descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            if (descriptor == null) throw new Exception("Missing world descriptor");
            if (descriptor.GetComponent<PipelineManager>() == null) descriptor.gameObject.AddComponent<PipelineManager>();
            // SDK preprocessing may save or modify the active scene; isolate that work.
            if (!EditorSceneManager.SaveScene(scene, "Assets/BirdGenerated/" + sceneName + "BuildValidation.unity")) throw new Exception("Could not save build fixture");
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
            string output = "../Validation/WorldBuild/" + sceneName + (target==BuildTarget.Android ? "_Android" : "") + ".vrcw";
            File.Copy(path, output, true);
            string hash;
            using (var stream = File.OpenRead(output)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            Finish(true, "SDK build-only " + target + " " + sceneName + " world bundle: " + new FileInfo(output).Length + " bytes; SHA256=" + hash + ". No client launch, upload, multiplayer or headset validation.");
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
    private const string Pending="Bird.World.SDK.Setup";
    private static double stableSince;
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
        System.IO.File.WriteAllText("world-sdk-setup-result.txt", "PENDING");
        UnityEditor.SessionState.SetBool(Pending,true);
        UnityEditor.SessionState.SetFloat(Pending+".Deadline",(float)UnityEditor.EditorApplication.timeSinceStartup+120);
        VRC.Editor.EnvConfig.SetActiveSDKDefines();
        ResumeSetup();
    }
    [UnityEditor.InitializeOnLoadMethod]
    private static void ResumeSetup()
    {
        if(!UnityEditor.SessionState.GetBool(Pending,false)) return;
        stableSince=0;
        UnityEditor.EditorApplication.update-=FinishSetup;
        UnityEditor.EditorApplication.update+=FinishSetup;
    }
    private static void FinishSetup()
    {
        double now=UnityEditor.EditorApplication.timeSinceStartup;
        if(now>UnityEditor.SessionState.GetFloat(Pending+".Deadline",0))
        {
            UnityEditor.SessionState.SetBool(Pending,false);
            System.IO.File.WriteAllText("world-sdk-setup-result.txt","FAIL: SDK/UdonSharp define setup did not settle within two minutes.");
            UnityEditor.EditorApplication.Exit(1); return;
        }
        if(UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating) { stableSince=0; return; }
        var group=UnityEditor.BuildPipeline.GetBuildTargetGroup(UnityEditor.EditorUserBuildSettings.activeBuildTarget);
        string defines=UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        // Let UdonSharp's own editor update install its symbol; do not exit before that reload.
        if(!defines.Contains("UDONSHARP") || !defines.Contains("VRC_SDK_VRCSDK3")) { stableSince=0; return; }
        if(stableSince==0) { stableSince=now; return; }
        if(now-stableSince<.5) return;
        UnityEditor.SessionState.SetBool(Pending,false);
        UnityEditor.EditorApplication.update-=FinishSetup;
        UnityEditor.AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("world-sdk-setup-result.txt","PASS: SDK and UdonSharp active-platform defines configured and compilation settled for "+group+".");
        UnityEditor.EditorApplication.Exit(0);
    }
}
