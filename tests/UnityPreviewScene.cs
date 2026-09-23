using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnityPreviewScene
{
    public static void Create()
    {
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bird desktop preview").AddComponent<BirdDesktopPreview>();
            if (!EditorSceneManager.SaveScene(scene, "Assets/BirdDesktopPreview.unity"))
                throw new Exception("Could not save preview scene");
            File.WriteAllText("core-checks-result.txt", "PASS: synthetic desktop preview compiled and scene generated; Unity " + Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("core-checks-result.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
