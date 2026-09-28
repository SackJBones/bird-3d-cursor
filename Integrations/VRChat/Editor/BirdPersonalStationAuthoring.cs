#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;

// One-time addition of an ordinary, self-contained prefab. Never regenerates architecture.
public static class BirdPersonalStationAuthoring
{
    public const string Folder = "Assets/BirdWorld/PersonalBird";
    public const string PrefabPath = Folder + "/Personal Bird station.prefab";
    [MenuItem("Bird/Coastal world/Add personal Bird station")]
    public static void AddToCoastalWorld()
    {
        if (File.Exists(PrefabPath)) throw new InvalidOperationException("Personal Bird already authored. Edit its prefab; do not recreate it.");
        string[] names = { "BirdPersonalStation", "BirdPointPresentation", "BirdAvatarHandInput", "BirdCursorState", "BirdSphereFit", "BirdRangeAdaptiveFilter", "BirdSphereCenterFilter" };
        foreach (string name in names)
        {
            var source = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/" + name + ".cs");
            if (source == null) throw new Exception("Restore source " + name);
            string path = "Assets/BirdWorld/Programs/" + name + ".asset";
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            if (program == null)
            {
                program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = source; AssetDatabase.CreateAsset(program, path);
            }
            else if (program.sourceCsScript != source) throw new Exception("Conflicting program: " + name);
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failed");
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        if (UnityEngine.Object.FindObjectOfType<BirdPersonalStation>(true) != null) throw new Exception("Station already present");
        var anchors = GameObject.Find("06 Experience anchors");
        if (anchors == null) throw new Exception("Missing authored experience anchors");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var shader = Shader.Find("Bird/LogicalDepth");
        if (shader == null) throw new Exception("Restore Bird logical-depth shader");
        var coreMaterial = new Material(shader) { color = Color.white, renderQueue = 2501 };
        coreMaterial.SetFloat("_ZWrite", 1); AssetDatabase.CreateAsset(coreMaterial, Folder + "/Point.mat");
        var haloMaterial = new Material(shader) { color = Color.white };
        haloMaterial.SetFloat("_UseVertexColor", 1); AssetDatabase.CreateAsset(haloMaterial, Folder + "/Halo.mat");
        var trailMaterial = new Material(haloMaterial);
        trailMaterial.SetFloat("_UseVertexDepthScale", 1); AssetDatabase.CreateAsset(trailMaterial, Folder + "/Trail.mat");
        var invitation = new Material(Shader.Find("Unlit/Color")) { color = new Color(.18f,.8f,.86f) };
        AssetDatabase.CreateAsset(invitation, Folder + "/Invitation.mat");
        var root = new GameObject("Personal Bird station");
        var station = root.AddUdonSharpComponent<BirdPersonalStation>();
        station.touchPoint = root.transform;
        var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = Vector3.one * .32f;
        Sphere("Touch to take Bird", root.transform, invitation, .09f).enabled = true;
        var canvas = new GameObject("Invitation", typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.transform.SetParent(root.transform, false);
        canvas.transform.localPosition = new Vector3(0,-.63f,-.295f); canvas.transform.localScale = Vector3.one * .001f;
        var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>(); label.transform.SetParent(canvas.transform, false);
        label.rectTransform.sizeDelta = new Vector2(1200,260); label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 52; label.color = new Color(.1f,.19f,.21f); label.raycastTarget = false;
        label.text = "Take a Bird\nTouch the light, or Use"; station.label = label;
        var rig = new GameObject("Personal Bird / local only"); rig.transform.SetParent(root.transform, false); station.personalRig = rig;
        for (int side = 0; side < 2; side++)
        {
            var hand = new GameObject(side == 0 ? "Left Bird" : "Right Bird"); hand.transform.SetParent(rig.transform, false);
            var input = Child<BirdAvatarHandInput>(hand.transform, "Avatar hand input");
            var cursor = Child<BirdCursorState>(hand.transform, "Geometric point");
            input.cursor = cursor; input.rightHand = side == 1; input.automaticSetup = true; input.littleFingerRootShare = .5f;
            cursor.fitter = Child<BirdSphereFit>(hand.transform, "Sphere fit");
            cursor.smoothing = true; cursor.clicksAllowed = false; cursor.useHandLimits = true;
            cursor.useSphereDirection = true; cursor.flatDirectionDegrees = 45; cursor.insideOutFullBlend = .25f;
            cursor.adaptiveFilter = Child<BirdRangeAdaptiveFilter>(hand.transform, "Adaptive output filter");
            cursor.centerFilter = Child<BirdSphereCenterFilter>(hand.transform, "Sphere center Kalman");
            var view = Child<BirdPointPresentation>(hand.transform, "Cursor and trail"); view.cursor = cursor;
            view.tint = side == 0 ? Color.cyan : new Color(1,.25f,.6f);
            view.core = Sphere("Marble", view.transform, coreMaterial, .032f);
            view.halo = new GameObject("Far locator").AddComponent<LineRenderer>(); view.halo.transform.SetParent(view.transform,false);
            view.halo.sharedMaterial = haloMaterial; view.halo.useWorldSpace = true; view.halo.loop = true; view.halo.positionCount = 32; view.halo.enabled = false;
            view.trailMesh = new GameObject("Depth-aware trail").AddComponent<MeshFilter>(); view.trailMesh.transform.SetParent(view.transform,false);
            view.trailRenderer = view.trailMesh.gameObject.AddComponent<MeshRenderer>(); view.trailRenderer.sharedMaterial = trailMaterial; view.trailRenderer.enabled = false;
            view.trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; view.trailRenderer.receiveShadows = false;
            if (side == 0) station.left = cursor; else station.right = cursor;
        }
        // All binding references are inside the prefab. No lab diagnostic dependencies.
        foreach (var proxy in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(station); vm.interactText = "Take / put away your Bird"; vm.proximity = 2;
        rig.SetActive(false);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); UnityEngine.Object.DestroyImmediate(root);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchors.transform);
        instance.transform.position = new Vector3(0,1.35f,-1.72f);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save station in coastal scene");
        AssetDatabase.SaveAssets();
    }
    static T Child<T>(Transform parent, string name) where T : UdonSharpBehaviour
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.AddUdonSharpComponent<T>();
    }
    static Renderer Sphere(string name, Transform parent, Material material, float diameter)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * diameter;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false; return renderer;
    }
}
#endif
