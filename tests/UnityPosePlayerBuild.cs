using System;
using System.IO;
using Bird3DCursor.Samples;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

public static class UnityPosePlayerBuild
{
    public static void Run()
    {
        try
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Bird pose docking preview"); root.AddComponent<BirdPosePreview>(); root.AddComponent<UnityPosePlayerSmoke>();
            if(!EditorSceneManager.SaveScene(scene,"Assets/PosePreview.unity")) throw new Exception("Scene save failed");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground=true; Directory.CreateDirectory("Build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/PosePreview.unity"},locationPathName="Build/BirdPosePreview.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.StrictMode});
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            if(Directory.GetFiles("Build","Bird3D.Manipulation.dll",SearchOption.AllDirectories).Length!=1) throw new Exception("Missing manipulation assembly");
            if(Directory.GetFiles("Build","Bird3D.Editor.dll",SearchOption.AllDirectories).Length!=0) throw new Exception("Editor assembly leaked");
            File.WriteAllText("pose-build-result.txt","PASS: Windows x64 Mono player includes Bird3D.Manipulation and excludes Bird3D.Editor; "+Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("pose-build-result.txt","FAIL: "+e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
