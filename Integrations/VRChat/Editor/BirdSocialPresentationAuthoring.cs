#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Components;

// One-time additive migration. Subsequent changes use the ordinary prefab.
public static class BirdSocialPresentationAuthoring
{
    [MenuItem("Bird/Coastal world/Add social Bird presentation")]
    public static async void AddFromMenu() { await AddToCoastalWorld(); }
    public static async Task AddToCoastalWorld()
    {
        string programPath = "Assets/BirdWorld/Programs/BirdSocialPresentation.asset";
        var source = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdSocialPresentation.cs");
        if (source == null) throw new Exception("Restore social presentation source");
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programPath);
        if (program == null)
        {
            program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript = source;
            AssetDatabase.CreateAsset(program, programPath);
        }
        else if (program.sourceCsScript != source) throw new Exception("Conflicting social presentation program");
        // Let the SDK's normal editor upgrade/import pass initialize a newly
        // created program asset before serializing any proxy against it.
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (program.ScriptVersion < UdonSharpProgramVersion.CurrentVersion && DateTime.UtcNow < deadline) await Task.Delay(100);
        if (program.ScriptVersion < UdonSharpProgramVersion.CurrentVersion) throw new Exception("SDK program initialization did not finish; retry after imports settle");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failed");
        var root = PrefabUtility.LoadPrefabContents(BirdPersonalStationAuthoring.PrefabPath);
        try
        {
            if (root.GetComponentInChildren<BirdSocialPresentation>(true) != null) throw new Exception("Social Bird already authored; edit the prefab normally");
            var station = root.GetComponent<BirdPersonalStation>();
            if (station == null) throw new Exception("Missing personal station");
            var template = new GameObject("Social Bird / player object template"); template.transform.SetParent(root.transform, false);
            template.AddComponent<VRCPlayerObject>();
            var bridge = template.AddUdonSharpComponent<BirdSocialPresentation>(); bridge.station = station;
            var originals = station.personalRig.GetComponentsInChildren<BirdPointPresentation>(true);
            foreach (var original in originals)
            {
                // Reuse the embodiment and its materials, with an independent
                // passive geometric point. No avatar adapter, fitter or filters.
                var view = UnityEngine.Object.Instantiate(original.gameObject, template.transform).GetComponent<BirdPointPresentation>();
                bool left = original.cursor == station.left; view.name = left ? "Remote left Bird" : "Remote right Bird";
                var point = new GameObject("Received geometric point"); point.transform.SetParent(view.transform, false);
                var cursor = point.AddUdonSharpComponent<BirdCursorState>(); cursor.clicksAllowed = false;
                view.cursor = cursor;
                if (left) { bridge.remoteLeft = cursor; bridge.localLeftView = original; bridge.remoteLeftView = view; }
                else { bridge.remoteRight = cursor; bridge.localRightView = original; bridge.remoteRightView = view; }
            }
            if (bridge.remoteLeft == null || bridge.remoteRight == null) throw new Exception("Two remote embodiments required");
            foreach (var proxy in template.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            PrefabUtility.SaveAsPrefabAsset(root, BirdPersonalStationAuthoring.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
}
#endif
