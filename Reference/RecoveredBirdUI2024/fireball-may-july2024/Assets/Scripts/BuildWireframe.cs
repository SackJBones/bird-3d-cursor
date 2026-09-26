using UnityEngine;
using AmazingAssets.WireframeShader;

public class BuildWireframe : MonoBehaviour
{
    public GameObject sourceObject; // The GameObject to get the Meshes from
    public float sizeThreshold = 1f; // Only include meshes larger than this threshold

    void Start()
    {
        // Get all MeshFilter components (which contain the Meshes) in sourceObject and its children
        MeshFilter[] meshFilters = sourceObject.GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        // Get the overall bounds of the sourceObject
        Bounds sourceBounds = GetTotalBounds(sourceObject);

        int count = 0;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            // Skip if the mesh is not readable or is small
            if (IsSmall(meshFilters[i].sharedMesh.bounds))
                continue;

            // Create a new CombineInstance for each Mesh
            combine[count] = new CombineInstance
            {
                mesh = meshFilters[i].sharedMesh,
                transform = meshFilters[i].transform.localToWorldMatrix
            };
            count++;
        }

        // Create a new Mesh and assign the combined Mesh to it
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);

        // Create a new GameObject to hold the combined Mesh
        GameObject combinedObject = new GameObject("CombinedMesh");
        combinedObject.transform.parent = transform;

        // Mesh GenerateWireframeMesh(bool normalizeEdges, bool tryQuad)
        // Generates new mesh with wireframe data baked inside uv4 buffer (note, inside shader uv4 coordinate of
        // a mesh is read using TEXCOORD3 semantic).
        // Resultant mesh is in 16 bit index buffer format if it has less than 65,535 vertices (21,845 triangles).
        // Otherwise mesh uses 32 bit index buffer format.

        // Texture2D GenerateWireframeTexture(bool useGeometryShader, int submeshIndex,
        // bool normalizeEdges, bool tryQuad,
        // float thickness, float smoothness, float diameter,
        // int resolution)
        // Generates wireframe texture.
        // bool useGeometryShader – If enabled, wireframe texture is generated using GeometryShaders and
        // source mesh does not need wireframe data to be baked inside it. For run-time use and build, project must
        // include Amazing Assets/Wireframe Shader/Shaders/Texture Exporter/Texture Exporter.shader file.
        // If this option is not used then wireframe texture is calculated from data baked inside a mesh.
        // int submeshIndex - Index of a submesh for which is rendered wireframe texture. Use value of -1 to export
        // submesh combined one texture.
        // bool normalizeEdges – Wireframe triangle edges will be approximately of the same width.
        // bool tryQuad – Renders wireframe in quad shape instead of a triangle. Result highly depends on a mesh
        // vertex & triangle layout.
        // float thickness, smoothness, diameter – Visual characteristic of the rendered wireframe.
        // int resolution - Texture resolution. Must be power of 2, in the range of 16 – 8192.

        // Generate wireframe mesh
        Mesh wireframeMesh = combinedMesh.GenerateWireframeMesh(
            false, // normalizeEdges
            true // tryQuad
        );

        // Generate wireframe texture
        Texture2D wireframeTexture = wireframeMesh.GenerateWireframeTexture(
            false, // useGeometryShader
            0, // submeshIndex
            true, // normalizeEdges
            true, // tryQuad
            0.01f, // thickness
            0f, // smoothness
            1f, // diameter
            1024 // resolution
        );

        // Add a MeshRenderer and MeshFilter component to the combined GameObject
        MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();

        // Assign the wireframe Mesh to the MeshFilter
        meshFilter.mesh = wireframeMesh;

        // Get wireframe shader
        Shader wireframeShader = Shader.Find("Hidden/Amazing Assets/Wireframe Shader/Vertex Lit/Transparent/Full");

        // Assign the wireframe shader to the MeshRenderer
        meshRenderer.material = new Material(wireframeShader);

        // Set rendering mode to transparent
        meshRenderer.material.SetFloat("_Mode", 3f);
        // Set color to 0 alpha
        meshRenderer.material.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
        // set smoothness to .8
        meshRenderer.material.SetFloat("_Smoothness", 0.8f);
        // set color to cyan
        meshRenderer.material.SetColor("_Wireframe_Color", Color.cyan);

        // Enable GPU instancing
        meshRenderer.material.enableInstancing = true;

        // Assign the wireframe Texture to the MeshRenderer
        meshRenderer.material.mainTexture = wireframeTexture;

        
        meshRenderer.material.SetFloat("_Wireframe_DynamicMaskType", 1f);
        meshRenderer.material.SetVector("_WireframeShaderMaskPlanePosition", Vector3.zero);
        meshRenderer.material.SetVector("_WireframeShaderMaskPlaneNormal", Vector3.down);

        //move the finished gameobject up one unit and scale it to 1/1000 of the original size
        combinedObject.transform.localScale = Vector3.one * 0.001f;
        combinedObject.transform.localPosition = new Vector3(0f, -5.5f, 0f);
    }

    Bounds GetTotalBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds();

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    bool IsSmall(Bounds meshBounds)
    {
        return meshBounds.size.magnitude < sizeThreshold;
    }
}