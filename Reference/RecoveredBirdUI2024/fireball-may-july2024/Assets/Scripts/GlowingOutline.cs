// Script to draw a glowing outline around an object.

// WARNING: The shader used here, "Unlit/Color," might not be included in the build by default on some platforms, causing the outliens to
// appear in the Unity Editor but not in the build. To fix this, in the Unity Editor, go to Edit -> Project Settings -> Graphics,
// and check if "Unlit/Color" is in the "Always Included Shaders" list.

using UnityEngine;

public class GlowingOutline : MonoBehaviour
{
    public Color outlineColor = Color.white;
    public float outlineWidth = 0.02f;
    // Auto-size only works correctly if the objects are lined up in the XY plane in world space when Start() is called.
    // This is due to use of unity's global-coordinate Bounds. Manually enter dimensions or set elsewhere and call Refresh() if needed.
    public bool autoSize = true; // Checkbox option
    // manual dimensions
    public float width = 1f;
    public float height = 1f;

    private LineRenderer lineRenderer;

    void Start()
    {
        // Get existing LineRenderer component or add a new one
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = false; // We'll use local space to ensure the outline stays with the object.
        lineRenderer.loop = true; // Ensures the outline is closed.
        Refresh();
    }

    public void Refresh() {
        // Set outline color and width
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = outlineColor;
        lineRenderer.startWidth = lineRenderer.endWidth = outlineWidth;

        // Automatically size the rectangle if enabled
        if (autoSize)
        {
            SizeRectangleToSelf();
        }
        else
        {
            // Manually set the rectangle size
            Vector3[] corners = new Vector3[4]
            {
                new Vector3(-width / 2f, -height / 2f, 0f),  // Bottom-left
                new Vector3(-width / 2f, height / 2f, 0f),  // Top-left
                new Vector3(width / 2f, height / 2f, 0f),  // Top-right
                new Vector3(width / 2f, -height / 2f, 0f),  // Bottom-right
            };
            lineRenderer.positionCount = corners.Length;
            lineRenderer.SetPositions(corners);
        }
    }

    private void SetOutlineWithBounds(Bounds bounds)
    {
        // convert to local coordinates for local line renderer with useWorldSpace = false
        Vector3[] corners = new Vector3[4]
        {
            transform.InverseTransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z)),  // Bottom-left
            transform.InverseTransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)),  // Top-left
            transform.InverseTransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)),  // Top-right
            transform.InverseTransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z)),  // Bottom-right
        };
        lineRenderer.positionCount = corners.Length;
        lineRenderer.SetPositions(corners);
        width = bounds.size.x;
        height = bounds.size.y;
    }

    private void SizeRectangleToSelf()
    {
        // use either a renderer component, if available, or if not, a collider component, to define the bounds of the rectangle
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            SetOutlineWithBounds(renderer.bounds);
        }
        else
        {
            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                SetOutlineWithBounds(collider.bounds);
            }
        }
    }
}
