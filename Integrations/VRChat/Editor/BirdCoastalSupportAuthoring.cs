#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit offline loft; exported content is an ordinary saved mesh and prefab.
public static class BirdCoastalSupportAuthoring
{
    public const string Folder = "Assets/BirdWorld/CoastalSupport";
    public const string MeshPath = Folder + "/Swept terrace bracket.asset";
    public const string PrefabPath = Folder + "/Pond terrace support.prefab";
    public const string ProfilePath = Folder + "/Support profile.asset";

    [MenuItem("Bird/Coastal world/Add pond support once")]
    public static void Add()
    {
        if (Directory.Exists(Folder)) throw new InvalidOperationException("Support assets already exist; edit the saved profile/prefab, do not recreate them.");
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var parent = GameObject.Find("04 Lower water and hidden lounge");
        if (parent == null || parent.transform.Find("Curved pond promenade") == null) throw new Exception("Author the pond first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BirdCoastalSupportProfile>(), ProfilePath);
        UpdateMesh();
        var root = new GameObject("Pond terrace support", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        try
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            root.GetComponent<MeshFilter>().sharedMesh = mesh; root.GetComponent<MeshCollider>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BirdCoastalWorldAuthoring.Folder + "/Materials/Plaster.mat");
            renderer.receiveGI = ReceiveGI.Lightmaps; renderer.scaleInLightmap = .5f;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    [MenuItem("Bird/Coastal world/Update pond support mesh only")]
    public static void UpdateMesh()
    {
        var p = AssetDatabase.LoadAssetAtPath<BirdCoastalSupportProfile>(ProfilePath);
        if (p == null || p.sections == null || p.sections.Length < 3 || p.circumferenceSegments < 16 || p.circumferenceSegments > 64 || p.samplesPerSpan < 1 || p.samplesPerSpan > 8)
            throw new InvalidOperationException("Invalid support profile.");
        var controls = p.sections.Select(s => new Vector4(s.x, s.height, s.halfWidth, s.halfLength)).ToArray();
        if (!Finite(p.centerZ) || controls.Any(s => !Finite(s.x) || !Finite(s.y) || !Finite(s.z) || !Finite(s.w) || s.z <= 0 || s.w <= 0) || controls.Where((s,i) => i > 0 && s.y <= controls[i-1].y).Any())
            throw new InvalidOperationException("Sections require finite, positive radii and strictly increasing heights.");
        var rings = new List<Vector4>();
        for (int i = 0; i < controls.Length - 1; i++) for (int j = 0; j < p.samplesPerSpan; j++)
        {
            float t = (float)j / p.samplesPerSpan;
            Vector4 a = controls[Mathf.Max(0,i-1)], b = controls[i], c = controls[i+1], d = controls[Mathf.Min(controls.Length-1,i+2)];
            rings.Add(.5f * (2*b + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t));
        }
        rings.Add(controls.Last());
        if (rings.Any(s => s.z <= 0 || s.w <= 0) || rings.Where((s,i) => i > 0 && s.y <= rings[i-1].y).Any()) throw new Exception("Interpolated support folds or collapses.");
        int n = p.circumferenceSegments; var v = new List<Vector3>(); var triangles = new List<int>();
        foreach (var s in rings) for (int j = 0; j < n; j++)
        { float a = j * Mathf.PI * 2 / n; v.Add(new Vector3(s.x + s.z * Mathf.Cos(a), s.y, p.centerZ + s.w * Mathf.Sin(a))); }
        for (int i = 0; i < rings.Count - 1; i++) for (int j = 0; j < n; j++)
        {
            int a=i*n+j, b=i*n+(j+1)%n, c=(i+1)*n+(j+1)%n, d=(i+1)*n+j;
            triangles.AddRange(new[]{a,d,c,a,c,b});
        }
        foreach (int ring in new[]{0,rings.Count-1})
        {
            int center=v.Count; v.Add(new Vector3(rings[ring].x,rings[ring].y,p.centerZ));
            for(int j=0;j<n;j++) v.Add(v[ring*n+j]);
            for(int j=0;j<n;j++)
            { int a=center+1+j,b=center+1+(j+1)%n; triangles.AddRange(ring==0?new[]{center,a,b}:new[]{center,b,a}); }
        }
        var mesh = new Mesh { name = "Swept terrace bracket" }; mesh.SetVertices(v); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh.uv=v.Select(q=>new Vector2(q.x,q.z)*.1f).ToArray();
        UnwrapParam.SetDefaults(out var parameters); parameters.packMargin*=4; Unwrapping.GenerateSecondaryUVSet(mesh,parameters);
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if(old==null) AssetDatabase.CreateAsset(mesh,MeshPath);
        else { EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(old); }
        foreach(var collider in UnityEngine.Object.FindObjectsOfType<MeshCollider>(true))
            if(AssetDatabase.GetAssetPath(collider.sharedMesh)==MeshPath){var saved=collider.sharedMesh;collider.sharedMesh=null;collider.sharedMesh=saved;}
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
    }
    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
#endif
