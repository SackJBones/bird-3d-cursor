#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// One-time authoring only. The result is an ordinary nested prefab with saved
// meshes and editable transforms; no generator or animation runs in the world.
public static class BirdCoastalGardenAuthoring
{
    public const string Folder = "Assets/BirdWorld/CoastalGarden";
    public const string PrefabPath = Folder + "/Pond garden corner.prefab";
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<Color> colors = new List<Color>();
    static readonly List<int> indices = new List<int>();

    [MenuItem("Bird/Coastal world/Add pond garden once")]
    public static void Add()
    {
        if (File.Exists(PrefabPath)) throw new InvalidOperationException("Garden already exists; edit the saved prefab and scene instance.");
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var parent = GameObject.Find("04 Lower water and hidden lounge");
        if (parent == null || parent.transform.Find("Curved pond promenade") == null) throw new Exception("Author the pond first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        string materials = BirdCoastalWorldAuthoring.Folder + "/Materials/";
        var plaster = AssetDatabase.LoadAssetAtPath<Material>(materials + "Plaster.mat");
        var warm = AssetDatabase.LoadAssetAtPath<Material>(materials + "Warm floor.mat");
        var foliage = new Material(Shader.Find("Bird/Coastal vertex color")) { name = "Painted coastal foliage", enableInstancing = true };
        AssetDatabase.CreateAsset(foliage, Folder + "/Painted coastal foliage.mat");
        var soil = new Material(Shader.Find("Unlit/Color")) { name = "Quiet soil", color = new Color(.19f, .23f, .17f) };
        AssetDatabase.CreateAsset(soil, Folder + "/Quiet soil.mat");

        var baseMesh = Save("White seat plinth", Capsule(2.8f, .78f, 0, .37f), true);
        var seatMesh = Save("Warm seat surface", Capsule(2.84f, .82f, .37f, .45f), true);
        var backMesh = Save("Low rounded back", Capsule(2.65f, .16f, .44f, .88f), true);
        var planterMesh = Save("Low oval planter", Oval(2.8f, 1.6f, 0, .32f, .88f), true);
        var soilMesh = Save("Inset soil", Oval(2.58f, 1.38f, .315f, .325f, 1), false);
        var broad = Save("Broad folded leaves", Leaves(false), false);
        var upright = Save("Upright folded leaves", Leaves(true), false);

        var root = new GameObject("Pond garden corner");
        try
        {
            Seat(root.transform, "Long waterside seat", new Vector3(2.9f, -2, 57), 25, 1, baseMesh, seatMesh, backMesh, plaster, warm);
            Seat(root.transform, "Short conversation seat", new Vector3(7.1f, -2, 56.7f), -28, .875f, baseMesh, seatMesh, backMesh, plaster, warm);
            Planting(root.transform, "Inland broad planting", new Vector3(1.7f, -2, 55.25f), 12, Vector3.one, planterMesh, soilMesh, broad, upright, plaster, soil, foliage, true);
            Planting(root.transform, "Eastern low planting", new Vector3(9.4f, -2, 55), -24, new Vector3(.78f, 1, .86f), planterMesh, soilMesh, broad, upright, plaster, soil, foliage, false);
            Planting(root.transform, "Western companion planting", new Vector3(-2.1f, -2, 54.5f), 35, new Vector3(.62f, .85f, .72f), planterMesh, soilMesh, broad, upright, plaster, soil, foliage, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.transform.localPosition = Vector3.zero;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    [MenuItem("Bird/Coastal world/Tighten first garden seating once")]
    public static void TightenFirstSeating()
    {
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var seat = root.transform.Find("Short conversation seat");
            var plants = root.transform.Find("Eastern low planting");
            if (seat.localPosition != new Vector3(8.1f, -2, 56.7f) || plants.localPosition != new Vector3(10.4f, -2, 55))
                throw new InvalidOperationException("This migration only applies to the first candidate. Keep later Inspector edits.");
            seat.localPosition += Vector3.left; plants.localPosition += Vector3.left;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    static void Seat(Transform parent, string name, Vector3 position, float yaw, float length,
        Mesh body, Mesh top, Mesh back, Material white, Material warm)
    {
        var root = Group(parent, name, position, yaw); root.localScale = new Vector3(length, 1, 1);
        Piece(root, "White plinth", body, white, true, true);
        Piece(root, "Warm sitting surface", top, warm, true, true);
        Piece(root, "Low back", back, white, true, true).localPosition = new Vector3(0, 0, -.32f);
    }

    static void Planting(Transform parent, string name, Vector3 position, float yaw, Vector3 scale,
        Mesh bed, Mesh earth, Mesh broad, Mesh upright, Material white, Material soil, Material foliage, bool tall)
    {
        var root = Group(parent, name, position, yaw); root.localScale = scale;
        Piece(root, "White planting base", bed, white, true, true);
        Piece(root, "Soil below leaves", earth, soil, false, false);
        var locations = new[] { new Vector3(-.77f, .325f, -.08f), new Vector3(.04f, .325f, .1f), new Vector3(.77f, .325f, -.13f) };
        for (int i = 0; i < locations.Length; i++)
        {
            var leaf = Piece(root, "Leaf group " + (i + 1), tall && i == 0 ? upright : broad, foliage, false, false);
            leaf.localPosition = locations[i]; leaf.localRotation = Quaternion.Euler(0, i * 113 + 17, 0);
            leaf.localScale = Vector3.one * (i == 1 ? .85f : i == 2 ? .69f : 1);
        }
    }

    static Transform Group(Transform parent, string name, Vector3 position, float yaw)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); return go.transform;
    }

    static Transform Piece(Transform parent, string name, Mesh mesh, Material material, bool collision, bool baked)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = baked ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveGI = baked ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes; renderer.scaleInLightmap = 1;
        if (collision) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | (baked ? StaticEditorFlags.ContributeGI : 0));
        return go.transform;
    }

    static Mesh Capsule(float length, float width, float bottom, float top)
    {
        var loop = new List<Vector2>(); float radius = width / 2, offset = length / 2 - radius;
        for (int end = 0; end < 2; end++) for (int i = 0; i <= 12; i++)
        {
            float angle = (-90 + end * 180 + i * 15) * Mathf.Deg2Rad;
            loop.Add(new Vector2((end == 0 ? offset : -offset) + Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
        }
        return Solid(loop, bottom, top, 1);
    }

    static Mesh Oval(float length, float width, float bottom, float top, float baseScale)
    {
        var loop = new List<Vector2>();
        for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16; loop.Add(new Vector2(Mathf.Cos(a) * length / 2, Mathf.Sin(a) * width / 2)); }
        return Solid(loop, bottom, top, baseScale);
    }

    static Mesh Solid(List<Vector2> loop, float bottom, float top, float baseScale)
    {
        Reset();
        for (int i = 0; i < loop.Count; i++)
        {
            var p = loop[i]; var q = loop[(i + 1) % loop.Count];
            var a = new Vector3(p.x * baseScale, bottom, p.y * baseScale); var b = new Vector3(q.x * baseScale, bottom, q.y * baseScale);
            var c = new Vector3(q.x, top, q.y); var d = new Vector3(p.x, top, p.y);
            Triangle(a, c, b, Color.white); Triangle(a, d, c, Color.white);
            Triangle(new Vector3(0, bottom, 0), a, b, Color.white);
            Triangle(new Vector3(0, top, 0), c, d, Color.white);
        }
        return Finish();
    }

    static Mesh Leaves(bool upright)
    {
        Reset();
        for (int i = 0; i < 9; i++)
        {
            float a = i * 2.399963f; float size = .76f + (i % 3) * .12f;
            var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = new Vector3(-direction.z, 0, direction.x);
            var start = Vector3.up * .015f;
            var center = direction * (upright ? .19f : .29f) * size + Vector3.up * (upright ? .59f : .28f) * size;
            var tip = direction * (upright ? .35f : .74f) * size + Vector3.up * (upright ? 1.12f : .47f) * size;
            var left = center - side * .19f * size; var right = center + side * .19f * size;
            var ridge = center + Vector3.up * .10f * size;
            Color tint = Color.Lerp(new Color(.20f, .36f, .16f), new Color(.42f, .52f, .24f), (i % 4) / 3f);
            LeafFace(start, left, ridge, tint * .84f); LeafFace(start, ridge, right, tint);
            LeafFace(left, tip, ridge, tint * .90f); LeafFace(ridge, tip, right, tint * 1.06f);
        }
        return Finish();
    }

    static void LeafFace(Vector3 a, Vector3 b, Vector3 c, Color color)
    { Triangle(a, b, c, color); Triangle(c, b, a, color * .85f); }
    static void Reset() { vertices.Clear(); colors.Clear(); indices.Clear(); }
    static void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
        for (int i = 0; i < 3; i++) { indices.Add(n + i); colors.Add(color.linear); }
    }
    static Mesh Finish()
    {
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.SetColors(colors);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.uv = vertices.Select(v => new Vector2(v.x, v.z)).ToArray(); return mesh;
    }
    static Mesh Save(string name, Mesh mesh, bool baked)
    {
        mesh.name = name;
        if (baked)
        {
            UnwrapParam.SetDefaults(out var parameters); parameters.packMargin *= 4;
            // Split vertices preserve exact geometry while adding per-corner UV2.
            mesh.uv2 = Unwrapping.GeneratePerTriangleUV(mesh, parameters);
        }
        AssetDatabase.CreateAsset(mesh, Folder + "/" + name + ".asset"); return mesh;
    }
}
#endif
