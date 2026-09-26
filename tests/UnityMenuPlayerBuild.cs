using System;
using System.IO;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnityMenuPlayerBuild
{
    public static void Run()
    {
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Bird menu preview");
            root.AddComponent<BirdMenuPreview>();
            root.AddComponent<UnityMenuPlayerSmoke>();
            if (!EditorSceneManager.SaveScene(scene,"Assets/MenuPreview.unity")) throw new Exception("Scene save failed");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground=true;
            Directory.CreateDirectory("Build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/MenuPreview.unity"}, locationPathName="Build/BirdMenuPreview.exe",
                target=BuildTarget.StandaloneWindows64, options=BuildOptions.StrictMode
            });
            if (report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            if (Directory.GetFiles("Build","Bird3D.UI.dll",SearchOption.AllDirectories).Length!=1)
                throw new Exception("UI assembly missing from player");
            if (Directory.GetFiles("Build","Bird3D.Editor.dll",SearchOption.AllDirectories).Length!=0)
                throw new Exception("Editor assembly in player");
            File.WriteAllText("menu-build-result.txt","PASS: Windows x64 Mono menu player built, UI assembly included, Bird editor assembly excluded; Unity "+Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch(Exception exception)
        {
            File.WriteAllText("menu-build-result.txt","FAIL: "+exception); Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
}
