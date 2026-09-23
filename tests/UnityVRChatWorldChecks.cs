using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

// Run only in a dedicated Worlds SDK project; does not upload or launch VRChat.
public static class UnityVRChatWorldChecks
{
    public static void Run()
    {
        try
        {
            const string scenePath = "Assets/BirdWorld/Scenes/BirdFeasibility.unity";
            if (File.Exists(scenePath)) throw new Exception("Scene already exists; preserve it rather than overwriting edits.");
            if (Application.unityVersion != "2022.3.22f1") throw new Exception("Use the VRChat-supported editor.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Feasibility floor";
            floor.transform.position = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(8, 0.2f, 8);
            var spawn = new GameObject("Local player spawn").transform;
            spawn.position = new Vector3(0, 0.1f, -2);
            var descriptor = new GameObject("VRCWorld").AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { spawn };
            var light = new GameObject("World light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Orientation landmark - Bird hand probe pending";
            marker.transform.position = new Vector3(0, 0.5f, 1);
            Directory.CreateDirectory("Assets/BirdWorld/Scenes");
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new Exception("Scene save failed.");
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            if (descriptor.spawns.Length != 1 || floor.GetComponent<Collider>() == null)
                throw new Exception("World scaffold validation failed.");
            // Resolving these types requires the actual Worlds and UdonSharp assemblies.
            var udonType = typeof(VRC.Udon.UdonBehaviour);
            var sharpType = typeof(UdonSharp.UdonSharpBehaviour);
            File.WriteAllText("world-checks-result.txt", "PASS: Worlds SDK and UdonSharp C# assemblies imported; floor, spawn and descriptor scene saved. No Udon program, ClientSim, client build or headset test performed. Unity " + Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("world-checks-result.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
