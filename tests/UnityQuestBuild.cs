using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

public static class UnityQuestBuild
{
    public static void Run()
    {
        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/QuestXRSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, "Assets/QuestXRSettings.asset");
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(settings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android),
                "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android)) throw new Exception("Oculus loader assignment failed");
            settings.SettingsForBuildTarget(BuildTargetGroup.Android).InitManagerOnStart = true;
            #if UNITY_2022_3_OR_NEWER
            Unity.XR.Oculus.OculusSettings oculus;
            if (!EditorBuildSettings.TryGetConfigObject("Unity.XR.Oculus.Settings", out oculus) || oculus == null)
            {
                oculus = AssetDatabase.LoadAssetAtPath<Unity.XR.Oculus.OculusSettings>("Assets/OculusSettings.asset");
                if (oculus == null)
                {
                    oculus = ScriptableObject.CreateInstance<Unity.XR.Oculus.OculusSettings>();
                    AssetDatabase.CreateAsset(oculus, "Assets/OculusSettings.asset");
                }
                EditorBuildSettings.AddConfigObject("Unity.XR.Oculus.Settings", oculus, true);
            }
            oculus.TargetQuest3 = true;
            EditorUtility.SetDirty(oculus);
            #endif
            AssetDatabase.SaveAssets();
            PlayerSettings.companyName = "Bird3D";
            PlayerSettings.productName = "Bird Quest Smoke";
            PlayerSettings.bundleVersion = Application.unityVersion.StartsWith("2022.") ? "1.1" : "1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "org.bird3d.questsmoke");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            #if UNITY_2022_3_OR_NEWER
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            #else
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            #endif
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Synthetic Bird preview").AddComponent<BirdDesktopPreview>();
            new GameObject("Quest smoke test").AddComponent<UnityQuestSmoke>();
            // Preview creates materials at runtime; keep its shaders in the player build.
            foreach (string shaderName in new[] { "Unlit/Color", "Sprites/Default" })
            {
                string path = "Assets/" + shaderName.Replace('/', '-') + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    var shader = Shader.Find(shaderName);
                    if (shader == null) throw new Exception("Missing preview shader: " + shaderName);
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                var keeper = new GameObject("Shader reference " + shaderName).AddComponent<MeshRenderer>();
                keeper.sharedMaterial = material;
                keeper.enabled = false;
            }
            EditorSceneManager.SaveScene(scene, "Assets/QuestSmoke.unity");
            Directory.CreateDirectory("Build");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/QuestSmoke.unity" }, locationPathName = "Build/BirdQuestSmoke.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Quest build failed: " + report.summary.result);
            File.WriteAllText("quest-build-result.txt", "PASS: ARM64 Quest smoke APK built with Unity " + Application.unityVersion + "; target API " + (int)PlayerSettings.Android.targetSdkVersion + "; bytes=" + new FileInfo("Build/BirdQuestSmoke.apk").Length);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("quest-build-result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
