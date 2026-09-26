#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;
using System.Linq;

[RequireComponent(typeof(Renderer),typeof(MeshFilter))]
public class ExtractSubmeshes : MonoBehaviour
{
    private static Mesh ExtractSubmesh(Mesh mesh,int submesh)
    {
        Mesh newMesh = new Mesh();
        SubMeshDescriptor descriptor = mesh.GetSubMesh(submesh);
        newMesh.vertices = GetRangeSubset(mesh.vertices, descriptor.firstVertex, descriptor.vertexCount);

        if(mesh.tangents != null && mesh.tangents.Length == mesh.vertices.Length)
        {
            newMesh.tangents = GetRangeSubset(mesh.tangents, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.boneWeights != null && mesh.boneWeights.Length == mesh.vertices.Length)
        {
            newMesh.boneWeights = GetRangeSubset(mesh.boneWeights, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.uv != null && mesh.uv.Length == mesh.vertices.Length)
        {
            newMesh.uv = GetRangeSubset(mesh.uv, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.uv2 != null && mesh.uv2.Length == mesh.vertices.Length)
        {
            newMesh.uv2 = GetRangeSubset(mesh.uv2, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.uv3 != null && mesh.uv3.Length == mesh.vertices.Length)
        {
            newMesh.uv3 = GetRangeSubset(mesh.uv3, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.uv4 != null && mesh.uv4.Length == mesh.vertices.Length)
        {
            newMesh.uv4 = GetRangeSubset(mesh.uv4, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.uv5 != null && mesh.uv5.Length == mesh.vertices.Length)
        {
            newMesh.uv5 = GetRangeSubset(mesh.uv5, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.uv6 != null && mesh.uv6.Length == mesh.vertices.Length)
        {
            newMesh.uv6 = GetRangeSubset(mesh.uv6, descriptor.firstVertex, descriptor.vertexCount);
        }

        if (mesh.uv7 != null && mesh.uv7.Length == mesh.vertices.Length)
        {
            newMesh.uv7 = GetRangeSubset(mesh.uv7, descriptor.firstVertex, descriptor.vertexCount);
        }

        if (mesh.uv8 != null && mesh.uv8.Length == mesh.vertices.Length)
        {
            newMesh.uv8 = GetRangeSubset(mesh.uv8, descriptor.firstVertex, descriptor.vertexCount);
        }

        if (mesh.colors != null && mesh.colors.Length == mesh.vertices.Length)
        {
            newMesh.colors = GetRangeSubset(mesh.colors, descriptor.firstVertex, descriptor.vertexCount);
        }

        if(mesh.colors32 != null && mesh.colors32.Length == mesh.vertices.Length)
        {
            newMesh.colors32 = GetRangeSubset(mesh.colors32, descriptor.firstVertex, descriptor.vertexCount);
        }

        var triangles = GetRangeSubset(mesh.triangles, descriptor.indexStart, descriptor.indexCount);
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] -= descriptor.firstVertex;
        }

        newMesh.triangles = triangles;

        if (mesh.normals != null && mesh.normals.Length == mesh.vertices.Length)
        {
            newMesh.normals = GetRangeSubset(mesh.normals, descriptor.firstVertex, descriptor.vertexCount);
        }
        else
        {
            newMesh.RecalculateNormals();
        }

        newMesh.Optimize();
        newMesh.OptimizeIndexBuffers();
        newMesh.RecalculateBounds();
        newMesh.name = mesh.name + $" Submesh {submesh}";
        return newMesh;
    }

    private static T[] GetRangeSubset<T>(T[] array, int start, int length)
    {
        return array.Skip(start).Take(length).ToArray();
    }

    public static string LastFilePath = "";

    [ContextMenu("Extract Meshes")]
    void Create()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if(meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No mesh exists on this gameObject");
            return;
        }

        if(meshFilter.sharedMesh.subMeshCount <= 1)
        {
            Debug.LogWarning("Mesh has <= 1 submesh components. No additional extraction required.");
            return;
        }

        if(LastFilePath == "")
        {
            LastFilePath = Application.dataPath;
        }

        for(int i = 0;i < meshFilter.sharedMesh.subMeshCount;i++)
        {
            string filePath = EditorUtility.SaveFilePanelInProject("Save Procedural Mesh", "Procedural Mesh", "asset", "",LastFilePath);
            if (filePath == "") continue;

            LastFilePath = Directory.GetDirectoryRoot(filePath);
            Debug.Log(LastFilePath);

            Mesh mesh = ExtractSubmesh(meshFilter.sharedMesh, i);
            AssetDatabase.CreateAsset(mesh, filePath);
        }
    }
}
#else
using UnityEngine;
public class ExtractSubmeshes : MonoBehaviour
{

}
#endif