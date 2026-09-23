using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;

public static class UnityVRChatProbeChecks
{
    public static void ValidateSaved()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdHandProbe.unity");
            var probe = UnityEngine.Object.FindObjectOfType<BirdHandDataProbe>();
            if (probe == null || probe.markers == null || probe.markers.Length != 32 || probe.status == null || probe.status.font == null)
                throw new Exception("Saved probe wiring is incomplete.");
            foreach (var marker in probe.markers)
                if (marker == null || marker.gameObject.activeSelf || marker.GetComponent<Collider>() != null)
                    throw new Exception("Saved marker must exist, start hidden and have no collider.");
            var program = UdonSharpProgramAsset.GetProgramAssetForClass(typeof(BirdHandDataProbe));
            if (program == null || program.SerializedProgramAsset == null || program.SerializedProgramAsset.RetrieveProgram() == null ||
                UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Saved Udon program is unavailable or has compile errors.");
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(probe);
            if (backing == null || backing.programSource != program) throw new Exception("Saved backing program mismatch.");
            File.WriteAllText("probe-reopen-result.txt", "PASS: saved scene reopened with 32 hidden collider-free markers, UI font and compiled Udon program references intact. No runtime execution test.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("probe-reopen-result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void Run()
    {
        try
        {
            const string output = "Assets/BirdWorld/Scenes/BirdHandProbe.unity";
            if (File.Exists(output)) throw new Exception("Probe scene exists; refusing to overwrite edits.");
            if (!AssetDatabase.IsValidFolder("Assets/BirdWorld/Programs"))
                AssetDatabase.CreateFolder("Assets/BirdWorld", "Programs");
            const string programPath = "Assets/BirdWorld/Programs/BirdHandDataProbe.asset";
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programPath);
            if (program == null)
            {
                program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdHandDataProbe.cs");
                if (program.sourceCsScript == null) throw new Exception("Copy the probe source and its stable meta before running.");
                AssetDatabase.CreateAsset(program, programPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("UdonSharp compilation reported errors.");
            var scene = EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
            var probe = new GameObject("Local avatar hand-bone diagnostic").AddUdonSharpComponent<BirdHandDataProbe>();
            probe.markers = new Transform[32];
            for (int i = 0; i < 32; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = (i < 16 ? "Left" : "Right") + " avatar bone " + (i % 16);
                marker.transform.SetParent(probe.transform);
                marker.transform.localScale = Vector3.one * 0.012f;
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.SetActive(false);
                probe.markers[i] = marker.transform;
            }
            var canvas = new GameObject("Hand probe panel", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.position = new Vector3(0, 1.5f, 0);
            canvas.transform.localScale = Vector3.one * 0.003f;
            var label = new GameObject("Hand probe status", typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();
            label.transform.SetParent(canvas.transform, false);
            label.rectTransform.sizeDelta = new Vector2(600, 250);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 30;
            label.text = "BIRD / HAND DATA PROBE\nWaiting for local player";
            probe.status = label;
            UdonSharpEditorUtility.CopyProxyToUdon(probe);
            if (program == null || program.SerializedProgramAsset == null || program.SerializedProgramAsset.RetrieveProgram() == null)
                throw new Exception("Probe did not produce an Udon program.");
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(probe);
            if (backing == null || backing.programSource != program) throw new Exception("Probe backing program mismatch.");
            if (probe.GetComponentsInChildren<Collider>(true).Length != 0) throw new Exception("Diagnostic markers retain colliders.");
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, output)) throw new Exception("Probe scene save failed.");
            File.WriteAllText("probe-checks-result.txt", "PASS: BirdHandDataProbe compiled to a real Udon program; 32 collider-free markers wired and scene saved. No ClientSim execution or real hand tracking validated.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("probe-checks-result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
