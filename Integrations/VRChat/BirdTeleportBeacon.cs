using UdonSharp;
using UnityEngine;

// Authored destination and presentation. Does not read hands or teleport players.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdTeleportBeacon : UdonSharpBehaviour
{
    [Tooltip("The circular target lies in this object's local XY plane.")]
    public float targetRadius = .82f;
    public Transform landing;
    [Tooltip("Pointing occlusion: real walls, floors and visible rail bars. Excludes invisible walking fences.")]
    public LayerMask occlusionLayers = (1 << 0) | (1 << 11) | (1 << 17);
    [Tooltip("Physical support and landing clearance, including invisible walking fences. Exclude players and triggers.")]
    public LayerMask solidLayers = (1 << 0) | (1 << 2) | (1 << 11);
    public float clearanceRadius = .32f;
    public Renderer ring;
    public Color idleColor = new Color(.12f, .55f, .62f);
    public Color hoverColor = new Color(.5f, 1, 1);
    public Color blockedColor = new Color(1, .6f, .12f);
    [HideInInspector] public float hitDistance;
    [HideInInspector] public Vector3 hitPoint, landingPoint;
    [HideInInspector] public int visualState; // 0 idle, 1 targeted, 2 landing blocked
    private MaterialPropertyBlock block;

    public bool Available()
    {
        Vector3 s = transform.lossyScale;
        return enabled && gameObject.activeInHierarchy && landing != null &&
            landing.gameObject.activeInHierarchy && Finite(targetRadius) && targetRadius > 0 &&
            Finite(s.x) && Finite(s.y) && Finite(s.z) &&
            Mathf.Abs(s.x) > .0001f && Mathf.Abs(s.y) > .0001f && Mathf.Abs(s.z) > .0001f;
    }

    public bool PointThrough(Vector3 origin, Vector3 point)
    {
        hitDistance = float.PositiveInfinity;
        if (!Available() || !FiniteVector(origin) || !FiniteVector(point)) return false;
        Vector3 a = transform.InverseTransformPoint(origin), b = transform.InverseTransformPoint(point);
        float dz = b.z - a.z;
        // An origin on the target plane has no meaningful pointing direction.
        if (!Finite(dz) || Mathf.Abs(a.z) < .0001f || Mathf.Abs(dz) < .0001f) return false;
        float t = -a.z / dz;
        if (t <= 0 || t > 1) return false;
        Vector3 local = a + (b - a) * t;
        if (local.x * local.x + local.y * local.y > targetRadius * targetRadius) return false;
        hitPoint = transform.TransformPoint(new Vector3(local.x, local.y, 0));
        Vector3 delta = hitPoint - origin;
        float distance = delta.magnitude;
        if (!Finite(distance) || distance < .001f || occlusionLayers.value == 0) return false;
        // A palm inside a wall must not exploit Raycast's inside-origin behavior.
        if (Physics.CheckSphere(origin, .005f, occlusionLayers, QueryTriggerInteraction.Ignore)) return false;
        if (Physics.Raycast(origin, delta / distance, Mathf.Max(0, distance - .01f), occlusionLayers, QueryTriggerInteraction.Ignore)) return false;
        hitDistance = distance;
        return true;
    }

    public bool CheckLanding(float height)
    {
        if (!Available() || !Finite(height) || !Finite(clearanceRadius) ||
            clearanceRadius < .1f || height < clearanceRadius * 2 || height > 8 || solidLayers.value == 0) return false;
        Vector3 foot = landing.position;
        if (!FiniteVector(foot)) return false;
        // Check support around the whole standing footprint, not only its center.
        for (int i = 0; i < 9; i++)
        {
            float angle = (i - 1) * Mathf.PI / 4;
            Vector3 offset = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * clearanceRadius;
            RaycastHit hit;
            if (!Physics.Raycast(foot + offset + Vector3.up * .3f, Vector3.down, out hit, .6f, solidLayers, QueryTriggerInteraction.Ignore) ||
                hit.normal.y < .95f || Mathf.Abs(hit.point.y - foot.y) > .08f) return false;
        }
        Vector3 bottom = foot + Vector3.up * (clearanceRadius + .04f);
        Vector3 top = foot + Vector3.up * (height - clearanceRadius + .04f);
        if (Physics.CheckCapsule(bottom, top, clearanceRadius, solidLayers, QueryTriggerInteraction.Ignore)) return false;
        landingPoint = foot + Vector3.up * .06f;
        return true;
    }

    public void ShowState(int state)
    {
        if (visualState == state && block != null) return;
        visualState = state;
        if (ring == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        ring.GetPropertyBlock(block);
        block.SetColor("_Color", state == 2 ? blockedColor : state == 1 ? hoverColor : idleColor);
        ring.SetPropertyBlock(block);
    }
    private void Start() { ShowState(0); }
    private void OnDisable() { ShowState(0); }
    private bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    private bool FiniteVector(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
}
