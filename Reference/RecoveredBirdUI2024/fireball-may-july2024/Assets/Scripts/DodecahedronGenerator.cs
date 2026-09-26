using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public static class Dodecahedron
{
    private static readonly float phi = (1f + Mathf.Sqrt(5f)) / 2f; // golden ratio

    private static readonly List<Vector3> vertices = new List<Vector3>() // vertices are defined according to the recipe on the Wikipedia article for "Regular dodecahedron"
    {
        // Orange vertices
        new Vector3(1f, 1f, 1f),
        new Vector3(-1f, 1f, 1f),
        new Vector3(1f, -1f, 1f),
        new Vector3(-1f, -1f, 1f),
        new Vector3(1f, 1f, -1f),
        new Vector3(-1f, 1f, -1f),
        new Vector3(1f, -1f, -1f),
        new Vector3(-1f, -1f, -1f),

        // Green vertices
        new Vector3(0f, phi, 1f/phi),
        new Vector3(0f, -phi, 1f/phi),
        new Vector3(0f, phi, -1f/phi),
        new Vector3(0f, -phi, -1f/phi),

        // Blue vertices
        new Vector3(1f/phi, 0f, phi),
        new Vector3(-1f/phi, 0f, phi),
        new Vector3(1f/phi, 0f, -phi),
        new Vector3(-1f/phi, 0f, -phi),

        // Pink vertices
        new Vector3(phi, 1f/phi, 0f),
        new Vector3(-phi, 1f/phi, 0f),
        new Vector3(phi, -1f/phi, 0f),
        new Vector3(-phi, -1f/phi, 0f)
    };

    public static List<Vector3> GetVertices()
    {
        // Normalize the vertices so that they form a unit dodecahedron
        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] = vertices[i].normalized;
        }
        return new List<Vector3>(vertices);
    }

    public static readonly int[][] faces = new int[][]
    {
        // Front, visible faces
        new int[] {0, 12, 2, 18, 16},
        new int[] {2, 12, 13, 3, 9},
        new int[] {2, 9, 11, 6, 18},
        new int[] {3, 19, 7, 11, 9},
        new int[] {6, 11, 7, 15, 14},
        new int[] {4, 16, 18, 6, 14},
        // Back, invisible faces
        new int[] {0, 16, 4, 10, 8},
        new int[] {0, 8, 1, 13, 12},
        new int[] {4, 14, 15, 5, 10},
        new int[] {1, 8, 10, 5, 17},
        new int[] {5, 15, 7, 19, 17},
        new int[] {1, 17, 19, 3, 13}
    };

    public static List<Vector3> GetFaceCenters()
    {
        List<Vector3> faceCenters = new List<Vector3>();
        List<Vector3> normedVertices = GetVertices();
        for (int i = 0; i < Dodecahedron.faces.Length; i++)
        {
            Vector3 faceCenter = new Vector3();
            for (int j = 0; j < Dodecahedron.faces[i].Length; j++)
            {
                faceCenter += normedVertices[Dodecahedron.faces[i][j]];
            }
            faceCenter /= Dodecahedron.faces[i].Length;
            faceCenters.Add(faceCenter);
        }
        return faceCenters;
    }
}

public class DodecahedronGenerator : MonoBehaviour
{
    // Class that verifies that there are exactly 12 child objects of the DodecahedronGenerator on startup, and then proceeds to arrange all of those children according to the centers of the Dodecahedron.faces of a unit dodecahedron.
    public float radius = 1f;
    // list of linerenderers
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    public bool linesEnabled = false;

    private void Start()
    {
        // Get the list of face centers of a unit dodecahedron
        List<Vector3> faceCenters = Dodecahedron.GetFaceCenters();

        // Verify that there are exactly 12 child objects of the DodecahedronGenerator
        if (transform.childCount != 12)
        {
            Debug.LogError("DodecahedronGenerator must have exactly 12 child objects!");
            return;
        }

        // Loop through all of the child objects of the DodecahedronGenerator
        for (int i = 0; i < transform.childCount; i++)
        {
            // Get the current child object
            Transform child = transform.GetChild(i);

            // set the position of the child  to the face center position
            child.localPosition = faceCenters[i] * radius;

            // Set the rotation of the current child object to face the corresponding face center of the unit dodecahedron
            //Quaternion targetRotation = Quaternion.LookRotation(faceCenters[i], Vector3.up);
            //child.rotation = targetRotation;

            // Get existing LineRenderer component or add a new one
            LineRenderer lineRenderer = child.gameObject.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = child.gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.useWorldSpace = false; // We'll use local space to ensure the outline stays with the object.
            lineRenderer.loop = true; // Ensures the outline is closed.
            lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
            lineRenderer.material.color = Color.white;
            
            // draw a line around the face
            int lineSubdivisions = 5;
            float faceScale = .3f;
            lineRenderer.positionCount = Dodecahedron.faces[i].Length * lineSubdivisions;
            for (int j = 0; j < Dodecahedron.faces[i].Length; j++)
            {
                lineRenderer.SetPosition(j * lineSubdivisions, (Dodecahedron.GetVertices()[Dodecahedron.faces[i][j]] - faceCenters[i]) * radius * faceScale);
            }
            // fill in the gaps between the vertices with more line positions so that the width changes are apparent
            for (int j = 0; j < Dodecahedron.faces[i].Length; j++)
            {
                Vector3 start = lineRenderer.GetPosition(j * lineSubdivisions);
                Vector3 end = lineRenderer.GetPosition((j * lineSubdivisions + lineSubdivisions) % lineRenderer.positionCount);
                // for k in range 0 to 4
                for (int k = 0; k < lineSubdivisions; k++)
                {
                    lineRenderer.SetPosition(j * lineSubdivisions + k, Vector3.Lerp(start, end, k / lineSubdivisions));
                }
            }
            AnimationCurve curve = new AnimationCurve();
            // make the width get larger and smaller again 5 times so that each pentagon's edges are thicker near their centers than near the vertices
            curve.AddKey(0f, .1f);
            curve.AddKey(.1f, 1f);
            curve.AddKey(.2f, .1f);
            curve.AddKey(.3f, 1f);
            curve.AddKey(.4f, .1f);
            curve.AddKey(.5f, 1f);
            curve.AddKey(.6f, .1f);
            curve.AddKey(.7f, 1f);
            curve.AddKey(.8f, .1f);
            curve.AddKey(.9f, 1f);
            curve.AddKey(1f, .1f);
            lineRenderer.widthCurve = curve;
            lineRenderer.widthMultiplier = .04f;
            lineRenderers.Add(lineRenderer);
        }
    }

    void Update()
    {
        SetLinesEnabled(linesEnabled);
    }

    public void SetLinesEnabled(bool enabled)
    {
        linesEnabled = enabled;
        // enable/disable all child elements
        // foreach (Transform child in transform)
        // {
        //     child.gameObject.SetActive(enabled);o
        // }
        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            lineRenderer.enabled = enabled;
        }
    }

    public void EnableLines()
    {
        linesEnabled = true;
    }

    public void DisableLines()
    {
        linesEnabled = false;
    }
}