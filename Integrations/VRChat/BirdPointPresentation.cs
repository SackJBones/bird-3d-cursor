using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Optional embodiment of a geometric point. No hand mapping or interaction policy.
// Plain inflation preserves a 32 mm marble throughout the 4 m working volume.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(100)]
public class BirdPointPresentation : UdonSharpBehaviour
{
    public BirdCursorState cursor;
    public Renderer core;
    public LineRenderer halo;
    public MeshFilter trailMesh;
    public MeshRenderer trailRenderer;
    public Color tint = Color.cyan;
    public Color selectedTint = Color.white;
    public float trailLifetime = .4f;
    [HideInInspector] public int trailCount;
    private MaterialPropertyBlock coreProperties, haloProperties;
    private Mesh ribbon;
    private Vector3[] history = new Vector3[32];
    private float[] times = new float[32];
    private Vector3[] vertices = new Vector3[64];
    private Color[] colors = new Color[64];
    private Vector2[] scales = new Vector2[64];
    private int first, revision;
    private float lastTime = -1;
    private Vector3 lastHead, lastRoot;

    public override void PostLateUpdate()
    {
        if (!enabled || !gameObject.activeInHierarchy || cursor == null || !cursor.poseValid ||
            !cursor.enabled || !cursor.gameObject.activeInHierarchy) { Clear(); return; }
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) { Clear(); return; }
        var head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        // Rebase on tracking/policy changes, suspension and player teleportation.
        // A legitimate long-range wrist sweep is not a teleport.
        float now = Time.realtimeSinceStartup;
        if (revision != cursor.historyRevision || (lastTime >= 0 &&
            (now - lastTime > .25f || now < lastTime || (head.position-lastHead).sqrMagnitude > 4 ||
            (cursor.handRoot-lastRoot).sqrMagnitude > 4))) Clear();
        revision = cursor.historyRevision; lastTime = now; lastHead = head.position; lastRoot = cursor.handRoot;
        Vector3 offset = cursor.position - head.position;
        float distance = Mathf.Max(.001f, offset.magnitude);
        float rendered = RenderDistance(distance), depthScale = distance / rendered;
        Vector3 projected = head.position + offset / depthScale;
        Color color = cursor.clicksAllowed && cursor.selected ? selectedTint : tint;
        float diameter = Diameter(distance);
        if (coreProperties == null) coreProperties = new MaterialPropertyBlock();
        if (haloProperties == null) haloProperties = new MaterialPropertyBlock();
        if (core != null)
        {
            core.enabled = true; core.transform.position = projected;
            core.transform.localScale = Vector3.one * (diameter / depthScale);
            coreProperties.SetColor("_Color", color); coreProperties.SetFloat("_BirdDepthScale", depthScale);
            core.SetPropertyBlock(coreProperties);
        }
        if (halo != null)
        {
            float blend = 1 - Mathf.Clamp01((diameter / distance - .0015f) / .003f);
            halo.enabled = distance > 4 && blend > 0;
            halo.startWidth = halo.endWidth = rendered * .0012f;
            halo.startColor = halo.endColor = new Color(color.r, color.g, color.b, .85f * blend);
            haloProperties.SetFloat("_BirdDepthScale", depthScale); halo.SetPropertyBlock(haloProperties);
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                halo.SetPosition(i, projected + head.rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * rendered * .002625f);
            }
        }
        DrawTrail(head.position, head.rotation, color, now);
    }
    private float RenderDistance(float d) { return d <= 100 ? d : 100 + 400 * (1 - 1 / (1 + (d - 100) / 400)); }
    private float Diameter(float d)
    {
        float t = Mathf.Clamp01(Mathf.Log(Mathf.Max(1, d / 4)) / Mathf.Log(5));
        return .032f * (1 + 8.5f * t * t * (3 - 2 * t)) * Mathf.Sqrt(Mathf.Max(1, d / 20));
    }
    private void DrawTrail(Vector3 eye, Quaternion rotation, Color tintColor, float now)
    {
        if (trailMesh == null || trailRenderer == null || trailLifetime <= 0) return;
        if (ribbon == null)
        {
            ribbon = new Mesh(); ribbon.MarkDynamic(); trailMesh.sharedMesh = ribbon;
            int[] triangles = new int[186];
            for (int i = 0; i < 31; i++)
            {
                int a = i * 2, k = i * 6;
                triangles[k] = a; triangles[k+1] = a+2; triangles[k+2] = a+1;
                triangles[k+3] = a+1; triangles[k+4] = a+2; triangles[k+5] = a+3;
            }
            ribbon.vertices = vertices; ribbon.triangles = triangles;
        }
        while (trailCount > 0 && now-times[first] >= trailLifetime) { first = (first+1)%32; trailCount--; }
        int last = (first+trailCount+31)%32;
        // Fixed work budget and bounded history. Keep a live head between samples.
        if (trailCount == 0 || now-times[last] >= 1f/60)
        {
            if (trailCount == 32) { first = (first+1)%32; trailCount--; }
            int slot = (first+trailCount)%32; history[slot] = cursor.position; times[slot] = now; trailCount++;
        }
        for (int i = 0; i < 32; i++)
        {
            int k = (first+Mathf.Min(i,trailCount-1))%32;
            Vector3 point = i >= trailCount-1 ? cursor.position : history[k];
            Vector3 delta = point-eye;
            float d = Mathf.Max(.001f,delta.magnitude), rd = RenderDistance(d), scale = d/rd;
            Vector3 projected = eye+delta/scale;
            int neighbor = (first+Mathf.Min(i+1,trailCount-1))%32;
            Vector3 tangent = i < trailCount-1 ? history[neighbor]-point :
                point-history[(first+Mathf.Max(0,trailCount-2))%32];
            Vector3 side = Vector3.Cross(delta.normalized,tangent.normalized);
            side = side.sqrMagnitude > .000001f ? side.normalized : rotation*Vector3.right;
            float blend = Mathf.Clamp01((d-4)/16); blend = blend*blend*(3-2*blend);
            float width = Mathf.Max(.002f, d*.0016f*blend) / scale;
            float alpha = .9f * Mathf.Clamp01(1-(now-times[k])/trailLifetime);
            if (i >= trailCount) alpha = 0;
            vertices[i*2] = trailMesh.transform.InverseTransformPoint(projected-side*width*.5f);
            vertices[i*2+1] = trailMesh.transform.InverseTransformPoint(projected+side*width*.5f);
            colors[i*2] = colors[i*2+1] = new Color(tintColor.r,tintColor.g,tintColor.b,alpha);
            scales[i*2] = scales[i*2+1] = new Vector2(scale,0);
        }
        ribbon.vertices = vertices; ribbon.colors = colors; ribbon.uv2 = scales; ribbon.RecalculateBounds();
        trailRenderer.enabled = trailCount > 1;
    }
    public void Clear()
    {
        trailCount = first = 0; lastTime = -1;
        if (core != null) core.enabled = false;
        if (halo != null) halo.enabled = false;
        if (trailRenderer != null) trailRenderer.enabled = false;
    }
    private void OnDisable() { Clear(); }
}
