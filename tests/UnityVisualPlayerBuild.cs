using System;
using System.IO;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnityVisualPlayerBuild
{
    public static void Run()
    {
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("Visual state menu"); go.AddComponent<BirdVisualStatesPreview>(); go.AddComponent<UnityVisualPlayerSmoke>(); EditorSceneManager.SaveScene(scene,"Assets/VisualStates.unity");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x); PlayerSettings.runInBackground=true; Directory.CreateDirectory("Build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/VisualStates.unity"},locationPathName="Build/BirdVisualStates.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.StrictMode });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed");
            if(Directory.GetFiles("Build","Bird3D.UI.dll",SearchOption.AllDirectories).Length!=1 || Directory.GetFiles("Build","Bird3D.Editor.dll",SearchOption.AllDirectories).Length!=0) throw new Exception("Assembly boundary failed");
            File.WriteAllText("visual-build-result.txt","PASS: Windows x64 Mono visual-state player; UI included and editor assembly excluded."); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("visual-build-result.txt","FAIL: "+e); EditorApplication.Exit(1); }
    }
}
