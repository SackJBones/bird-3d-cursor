using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnityPlayerBuild
{
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bird package smoke test").AddComponent<UnityPlayerSmoke>();
            if (!EditorSceneManager.SaveScene(scene, "Assets/BirdPlayerSmoke.unity")) throw new Exception("Could not save validation scene");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            Directory.CreateDirectory("Build");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/BirdPlayerSmoke.unity" },
                locationPathName = "Build/BirdPlayerSmoke.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Player build failed: " + report.summary.result);
            if (Directory.GetFiles("Build", "Bird3D.Runtime.dll", SearchOption.AllDirectories).Length != 1)
                throw new Exception("Expected one Bird runtime assembly in player output");
            if (Directory.GetFiles("Build", "Bird3D.Editor.dll", SearchOption.AllDirectories).Length != 0)
                throw new Exception("Bird editor assembly leaked into player output");
            File.WriteAllText("core-checks-result.txt", "PASS: Windows Mono player built with runtime assembly and no Bird editor assembly; Unity " + Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("core-checks-result.txt", "FAIL: " + exception);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
