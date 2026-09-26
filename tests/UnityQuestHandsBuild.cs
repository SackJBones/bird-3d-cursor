#if BIRD_OPENXR_ENABLED
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEngine.XR.Hands.OpenXR;

public static class UnityQuestHandsBuild
{
    public static void Run()
    {
        try
        {
            var general = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/QuestXRSettings.asset");
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(general, "Assets/QuestXRSettings.asset");
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, general, true);
            if (!general.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) general.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(general.ManagerSettingsForBuildTarget(BuildTargetGroup.Android),
                "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android)) throw new Exception("OpenXR loader assignment failed");
            general.SettingsForBuildTarget(BuildTargetGroup.Android).InitManagerOnStart = true;
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (xr == null) throw new Exception("OpenXR settings absent");
            var hands = xr.GetFeature<HandTracking>();
            var quest = xr.GetFeature<MetaQuestFeature>();
            var touch = xr.GetFeature<OculusTouchControllerProfile>();
            if (hands == null || quest == null || touch == null) throw new Exception("OpenXR features absent");
            hands.enabled = quest.enabled = touch.enabled = true;
            quest.AddTargetDevice("eureka", "Quest 3", true);
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(hands); EditorUtility.SetDirty(quest); EditorUtility.SetDirty(touch); EditorUtility.SetDirty(xr);
            PlayerSettings.companyName = "Bird3D";
            PlayerSettings.productName = "Bird Live Hands";
            PlayerSettings.bundleVersion = "0.9";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "org.bird3d.livehands");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            QualitySettings.antiAliasing = 4;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Vista lighting is created after tracking starts. Keep its fog
            // variant in the serialized scene so Android stripping retains it.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .00055f;
            string checks = UnityQuestHandsChecks.Run() + "\n" + UnityDepthVisualChecks.CheckMath() + "\n" + UnityQuestTraceChecks.Run() + "\n" + UnityQuestReplayChecks.RunOptional();
            File.WriteAllText("hands-math-result.txt", checks);
            Debug.Log(checks);
            new GameObject("Bird live hands comparison").AddComponent<UnityQuestHands>();
            foreach (string shaderName in new[] { "Unlit/Color", "Sprites/Default", "Standard", "Bird/LogicalDepth" })
            {
                string path = "Assets/" + shaderName.Replace('/', '-') + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find(shaderName));
                    AssetDatabase.CreateAsset(material, path);
                }
                var keeper = new GameObject("Shader reference").AddComponent<MeshRenderer>();
                keeper.sharedMaterial = material;
                keeper.enabled = false;
            }
            EditorSceneManager.SaveScene(scene, "Assets/QuestHands.unity");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Build");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/QuestHands.unity" }, locationPathName = "Build/BirdLiveHands.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            File.WriteAllText("hands-build-result.txt", "PASS: Unity " + Application.unityVersion + " ARM64 OpenXR live hands APK; bytes=" + new FileInfo("Build/BirdLiveHands.apk").Length + "\n" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("hands-build-result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
#endif
