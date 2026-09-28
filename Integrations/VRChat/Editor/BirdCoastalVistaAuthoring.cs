#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Additive scenery. No runtime generator, world collision, or Bird dependencies.
public static class BirdCoastalVistaAuthoring
{
    public const string Folder = "Assets/BirdWorld/CoastalVista";
    public const string ProfilePath = Folder + "/Coast profile.asset";
    public const string PrefabPath = Folder + "/Coastal vista.prefab";
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<Color> colors = new List<Color>();
    static readonly List<int> indices = new List<int>();

    [MenuItem("Bird/Coastal world/Add coastal vista once")]
    public static void Add()
    {
        if (File.Exists(PrefabPath)) throw new InvalidOperationException("Vista already exists. Edit its prefab/material/profile; do not recreate it.");
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var profile = ScriptableObject.CreateInstance<BirdCoastalVistaProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        UpdateMeshes();
        var shader = Shader.Find("Bird/Coastal vertex color");
        if (shader == null) throw new Exception("Missing coastal vertex shader.");
        var material = new Material(shader) { name = "Coastal painted color", enableInstancing = true };
        AssetDatabase.CreateAsset(material, Folder + "/Coastal painted color.mat");
        var sky = new Material(Shader.Find("Bird/Coastal daylight sky")) { name = "Quiet coastal daylight" };
        SetSkyColors(sky);
        AssetDatabase.CreateAsset(sky, Folder + "/Quiet coastal daylight.mat");
        var root = new GameObject("Coastal vista");
        AddMesh(root.transform, "Middle coast", "Middle coast", material, new Vector3(200, -30.8f, 1150), 12);
        AddMesh(root.transform, "Far coast", "Far coast", material, new Vector3(1100, -30.8f, 2250), -8);
        AddMesh(root.transform, "Open sea", "Open sea", material, new Vector3(0, -30.8f, 0), 0);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); UnityEngine.Object.DestroyImmediate(root);
        var parent = GameObject.Find("05 Coastal ridge and distant island").transform;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        instance.transform.SetParent(parent, false);
        // Hide the finite rectangular sea; retain the original object and transform for editing/history.
        var oldSea = parent.Find("Quiet sea").GetComponent<MeshRenderer>(); oldSea.enabled = false;
        GameObjectUtility.SetStaticEditorFlags(oldSea.gameObject, GameObjectUtility.GetStaticEditorFlags(oldSea.gameObject) & ~StaticEditorFlags.ContributeGI);
        PrefabUtility.RecordPrefabInstancePropertyModifications(oldSea);
        PrefabUtility.RecordPrefabInstancePropertyModifications(oldSea.gameObject);
        RenderSettings.skybox = sky;
        // Ambient light remains Flat and the saved sun/fill/probe settings are untouched.
        // Re-bake before delivery so the ordinary reflection environment matches the sky.
        var camera = GameObject.Find("World camera settings").GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.Skybox;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    [MenuItem("Bird/Coastal world/Refine first vista daylight once")]
    public static void RefineDaylight()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Quiet coastal daylight.mat");
        if (sky == null || sky.shader.name != "Skybox/Procedural") throw new Exception("This migration only refines the initial procedural-sky candidate. Edit current saved assets normally.");
        sky.shader = Shader.Find("Bird/Coastal daylight sky"); sky.shaderKeywords = new string[0]; SetSkyColors(sky); EditorUtility.SetDirty(sky);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { root.transform.Find("Far coast").localPosition = new Vector3(1100, -30.8f, 2250); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        UpdateMeshes(); AssetDatabase.SaveAssets();
    }
    static void SetSkyColors(Material sky)
    {
        sky.SetColor("_Zenith", new Color(.29f, .52f, .75f));
        sky.SetColor("_Horizon", new Color(.76f, .84f, .88f));
        sky.SetColor("_Ground", new Color(.53f, .69f, .75f));
    }

    [MenuItem("Bird/Coastal world/Update vista profile meshes only")]
    public static void UpdateMeshes()
    {
        var p = AssetDatabase.LoadAssetAtPath<BirdCoastalVistaProfile>(ProfilePath);
        if (p == null) throw new Exception("Create the coastal vista first.");
        Save("Middle coast", Coast(p.middleCoast, p.middleRock, p.middleLand));
        Save("Far coast", Coast(p.farCoast, p.farRock, p.farLand));
        Save("Open sea", Sea(p)); AssetDatabase.SaveAssets();
    }
    static void AddMesh(Transform parent, string name, string mesh, Material material, Vector3 position, float yaw)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + mesh + ".asset");
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }
    static Mesh Coast(Vector4[] sections, Color rock, Color land)
    {
        if (sections == null || sections.Length < 3) throw new Exception("A coastline needs at least three cross sections.");
        for (int i = 0; i < sections.Length; i++)
            if (sections[i].z <= 0 || (i > 0 && sections[i].x <= sections[i - 1].x) || !Finite(sections[i])) throw new Exception("Coast sections need finite values, positive width and increasing positions.");
        Reset();
        float[] band = { -1, -.76f, -.37f, .06f, .43f, .78f, 1 };
        Func<int, int, Vector3> point = (i, j) => {
            var s = sections[i]; float v = band[j];
            float height = Mathf.Pow(Mathf.Max(0, 1 - Mathf.Abs(v)), .72f) * (s.w + 4) - 4;
            float shoulder = Mathf.Sin(i * 2.1f + j * 1.7f) * 8 * (1 - Mathf.Abs(v));
            // Keep each cross-section on its longitudinal plane. Jittering x can
            // fold narrow tapered ends over themselves and invert triangles.
            return new Vector3(s.x, height + shoulder, s.y + v * s.z);
        };
        for (int i = 0; i < sections.Length - 1; i++) for (int j = 0; j < band.Length - 1; j++)
        {
            var a = point(i, j); var b = point(i + 1, j); var c = point(i + 1, j + 1); var d = point(i, j + 1);
            if ((i + j) % 2 == 0) { Face(a, d, b, rock, land); Face(b, d, c, rock, land); }
            else { Face(a, d, c, rock, land); Face(a, c, b, rock, land); }
        }
        return Finish();
    }
    static bool Finite(Vector4 v) { return !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude); }
    static void Face(Vector3 a, Vector3 b, Vector3 c, Color rock, Color land)
    {
        var normal = Vector3.Cross(b - a, c - a).normalized;
        float slope = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .9f, normal.y));
        float height = Mathf.Clamp01(((a.y + b.y + c.y) / 3 - 10) / 45);
        Color tint = Color.Lerp(rock, land, slope * height * .8f);
        float lighting = .82f + .18f * Mathf.Max(0, Vector3.Dot(normal, new Vector3(-.4f, .8f, -.45f).normalized));
        tint *= lighting; tint.a = 1; Triangle(a, b, c, tint, tint, tint);
    }
    static Mesh Sea(BirdCoastalVistaProfile p)
    {
        if (p.waterRadius < 3000 || p.waterRadius > 8000) throw new Exception("Keep the sea within the 10 km reference camera and outside the distant coast.");
        Reset(); int segments = 96; float[] radius = { 0, 300, 900, 2200, p.waterRadius };
        for (int ring = 0; ring < radius.Length - 1; ring++) for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
            var p0 = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius[ring]; var p1 = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius[ring];
            var p2 = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius[ring + 1]; var p3 = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius[ring + 1];
            Color inner = Color.Lerp(p.nearWater, p.farWater, Mathf.Clamp01(radius[ring] / 3500));
            Color outer = Color.Lerp(p.nearWater, p.farWater, Mathf.Clamp01(radius[ring + 1] / 3500));
            if (ring > 0) Triangle(p0, p1, p2, inner, inner, outer);
            Triangle(p0, p2, p3, inner, outer, outer);
        }
        return Finish();
    }
    static void Reset() { vertices.Clear(); colors.Clear(); indices.Clear(); }
    static void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
    {
        // Inspector palette is sRGB, while mesh vertex channels do not receive
        // the automatic color-property conversion of material uniforms.
        int i = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); colors.Add(ca.linear); colors.Add(cb.linear); colors.Add(cc.linear); indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
    }
    static Mesh Finish() { var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh; }
    static void Save(string name, Mesh mesh)
    {
        mesh.name = name; string path = Folder + "/" + name + ".asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) AssetDatabase.CreateAsset(mesh, path);
        else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
    }
}
#endif
