#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// A scoped offline edit of the original lookout rail, using its saved boundary.
public static class BirdLookoutRailAuthoring
{
    public const string ObjectPath="03 Supported coastal terraces/Lookout edge";
    public const string MeshPath=BirdCoastalWorldAuthoring.Folder+"/Meshes/03 Supported coastal terraces-018-Lookout edge.asset";
    public const string GuardPath=BirdCoastalWorldAuthoring.Folder+"/Meshes/03 Supported coastal terraces-019-Lookout edge safety boundary.asset";

    public static List<Vector2[]> SavedSegments()
    {
        var guard=AssetDatabase.LoadAssetAtPath<Mesh>(GuardPath);
        if(guard==null||guard.vertexCount!=48*12)throw new InvalidOperationException("Expected original 48-panel lookout boundary. Inspect authored geometry before updating.");
        var v=guard.vertices;var segments=new List<Vector2[]>();
        for(int i=0;i<v.Length;i+=12)
        {
            if(Mathf.Abs(v[i].y)>.001f||Mathf.Abs(v[i+1].y)>.001f||Mathf.Abs(v[i+2].y-1.1f)>.001f)
                throw new InvalidOperationException("Unexpected boundary panel layout.");
            segments.Add(new[]{new Vector2(v[i].x,v[i].z),new Vector2(v[i+1].x,v[i+1].z)});
        }
        var paths=BirdCoastalRailMesh.Paths(segments);
        if(paths.Count!=1||Vector2.Distance(paths[0][0],paths[0].Last())>.001f)
            throw new InvalidOperationException("Lookout boundary must be one closed path.");
        return segments;
    }

    [MenuItem("Bird/Coastal world/Update lookout rail mesh from saved boundary")]
    public static void UpdateMesh()
    {
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if(saved==null)throw new InvalidOperationException("Missing original lookout rail mesh.");
        var segments=SavedSegments();
        var mesh=BirdCoastalRailMesh.Build(segments,segments.Select(s=>s[0]).ToList(),0,1.05f,.13f,.065f,1.06f);
        try
        {
            UnwrapParam.SetDefaults(out var parameters);parameters.packMargin*=4;
            if(!Unwrapping.GenerateSecondaryUVSet(mesh,parameters))throw new InvalidOperationException("Rail UV2 generation failed.");
            saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;
            saved.normals=mesh.normals;saved.triangles=mesh.triangles;saved.uv=mesh.uv;saved.uv2=mesh.uv2;saved.bounds=mesh.bounds;
            EditorUtility.SetDirty(saved);
        }
        finally{UnityEngine.Object.DestroyImmediate(mesh);}
        foreach(var collider in UnityEngine.Object.FindObjectsOfType<MeshCollider>(true))
            if(collider.sharedMesh==saved){collider.sharedMesh=null;collider.sharedMesh=saved;}
        Physics.SyncTransforms();AssetDatabase.SaveAssets();
    }

    [MenuItem("Bird/Coastal world/Finish original lookout rail once")]
    public static void FinishOriginal()
    {
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var rail=GameObject.Find(ObjectPath);
        if(rail==null||rail.GetComponent<MeshFilter>().sharedMesh.vertexCount!=3456)
            throw new InvalidOperationException("Original segmented rail already replaced or edited. Use explicit mesh update or Inspector editing.");
        UpdateMesh();
        // Preserve every other saved lighting choice; scene overrides remain editable.
        var renderer=rail.GetComponent<MeshRenderer>();renderer.scaleInLightmap=4;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}
#endif
