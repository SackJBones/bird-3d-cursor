using System;
using System.IO;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnityMapPlayerBuild
{
    public static void Run()
    {
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Bird Map Preview"); root.AddComponent<BirdMapPreview>(); root.AddComponent<UnityMapPlayerSmoke>();
            if(!EditorSceneManager.SaveScene(scene,"Assets/MapPreview.unity")) throw new Exception("Scene save failed");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x); PlayerSettings.runInBackground=true;
            Directory.CreateDirectory("Build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/MapPreview.unity"},locationPathName="Build/BirdMapPreview.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.StrictMode });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            if(Directory.GetFiles("Build","Bird3D.UI.dll",SearchOption.AllDirectories).Length!=1 || Directory.GetFiles("Build","Bird3D.Editor.dll",SearchOption.AllDirectories).Length!=0) throw new Exception("Player assembly boundary");
            File.WriteAllText("map-build-result.txt","PASS: Windows x64 Mono map player, Bird3D.UI included, editor assembly excluded."); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("map-build-result.txt","FAIL: "+e); EditorApplication.Exit(1); }
    }
}
