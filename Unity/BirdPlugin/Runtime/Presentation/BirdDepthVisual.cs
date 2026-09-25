using UnityEngine;

namespace Bird3DCursor.Presentation
{
    /// <summary>Example experience choices, independent of Bird geometry.</summary>
    [System.Serializable]
    public sealed class BirdDepthStyle
    {
        [Min(.001f)] public float nearDiameter = .032f;
        [Min(.01f)] public float workingDistance = 4f;
        [Min(1.01f)] public float inflationEndRatio = 5f;
        [Min(1f)] public float inflationFactor = 8f;
        [Range(0f, .95f)] public float farGrowthExponent = .5f;
        [Min(.001f)] public float growthLagSeconds = .22f;
        [Min(.0001f)] public float nearTrailWidth = .002f;
        [Min(.0001f)] public float farTrailAngleRadians = .0008f;
        public bool showFarLocator = true;
        [Min(.0001f)] public float locatorAngleRadians = .0035f;
    }

    /// <summary>Visual-only long-range cursor and trail. Logical interaction
    /// coordinates are never modified. Reprojects astronomical positions into
    /// a bounded render shell; hosts must account for far-world occlusion.</summary>
    public sealed class BirdDepthVisual : MonoBehaviour
    {
        public enum SizeMode { Fixed, Inflation, InflationWithLag }
        public SizeMode sizeMode = SizeMode.InflationWithLag;
        public Color tint = Color.cyan;
        public bool showTrail = true;
        public BirdDepthStyle style = new BirdDepthStyle();
        static readonly BirdDepthStyle DefaultStyle = new BirdDepthStyle();
        public const float NearDiameter = .032f;
        public const float WorkingDistance = 4f;
        const int Capacity = 96;
        readonly Vector3[] history = new Vector3[Capacity];
        readonly float[] times = new float[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity*2];
        readonly Color[] colors = new Color[Capacity*2];
        int count;
        float lastSampleTime;
        float diameter = NearDiameter;
        bool ready;
        Transform core;
        LineRenderer halo;
        Mesh ribbon;
        Renderer ribbonRenderer;
        Material coreMaterial, lineMaterial;
        public float CurrentDiameter { get { return diameter; } }

        void EnsureReady()
        {
            if (ready) return;
            ready = true;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            core = dot.transform;
            core.SetParent(transform, false);
            coreMaterial = new Material(Shader.Find("Unlit/Color"));
            coreMaterial.color = tint;
            dot.GetComponent<Renderer>().sharedMaterial = coreMaterial;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            halo = new GameObject("Far locator outline").AddComponent<LineRenderer>();
            halo.transform.SetParent(transform, false);
            halo.sharedMaterial = lineMaterial;
            halo.positionCount = 40;
            halo.loop = true;
            halo.useWorldSpace = true;
            halo.startColor = halo.endColor = new Color(tint.r, tint.g, tint.b, .55f);
            var trail = new GameObject("Depth-aware ribbon");
            trail.transform.SetParent(transform, false);
            ribbon = new Mesh { name = "Bird visual trail" };
            ribbon.MarkDynamic();
            trail.AddComponent<MeshFilter>().sharedMesh = ribbon;
            ribbonRenderer = trail.AddComponent<MeshRenderer>();
            ribbonRenderer.sharedMaterial = lineMaterial;
            var triangles = new int[(Capacity-1)*6];
            for (int i = 0; i < Capacity-1; i++)
            {
                int a = i*2, k = i*6;
                triangles[k] = a; triangles[k+1] = a+2; triangles[k+2] = a+1;
                triangles[k+3] = a+1; triangles[k+4] = a+2; triangles[k+5] = a+3;
            }
            ribbon.vertices = vertices;
            ribbon.triangles = triangles;
        }

        // Fixed world size throughout the natural working volume. Inflation is
        // confined to 4-20m; farther away, angular size resumes decreasing.
        public static float TargetDiameter(float distance, SizeMode mode, BirdDepthStyle settings = null)
        {
            var s = settings ?? DefaultStyle;
            float working = Mathf.Max(.01f, s.workingDistance);
            float endRatio = Mathf.Max(1.01f, s.inflationEndRatio);
            if (mode == SizeMode.Fixed || distance <= working) return s.nearDiameter;
            float t = Mathf.Clamp01(Mathf.Log(distance/working) / Mathf.Log(endRatio));
            float inflation = 1 + (s.inflationFactor-1)*t*t*(3-2*t);
            float farGrowth = Mathf.Pow(Mathf.Max(1, distance/(working*endRatio)), s.farGrowthExponent);
            return s.nearDiameter*inflation*farGrowth;
        }

        public static float AdvanceDiameter(float current, float target, float distance, float dt, SizeMode mode, BirdDepthStyle settings = null)
        {
            // Inward resize is immediate. No stale giant visual can enter the
            // working volume, including on a one-frame far-to-palm return.
            var s = settings ?? DefaultStyle;
            if (distance <= s.workingDistance) return s.nearDiameter;
            if (mode != SizeMode.InflationWithLag || target <= current) return target;
            return Mathf.Lerp(current, target, 1-Mathf.Exp(-Mathf.Max(0, dt)/Mathf.Max(.001f,s.growthLagSeconds)));
        }

        public static float RenderDistance(float distance)
        {
            // Identity through 100m, continuous with matching first derivative.
            // Retain the true direction; no disappearance at the camera far clip.
            if (distance <= 100) return distance;
            return 100 + 400*(1 - 1/(1 + (distance-100)/400));
        }

        public static Vector3 Project(Vector3 logical, Vector3 eye)
        {
            Vector3 delta = logical-eye;
            float d = delta.magnitude;
            return d > 0 ? eye + delta*(RenderDistance(d)/d) : logical;
        }

        public static float TrailWidth(float distance, BirdDepthStyle settings = null)
        {
            var s = settings ?? DefaultStyle;
            float working = Mathf.Max(.01f,s.workingDistance);
            if (distance <= working) return s.nearTrailWidth;
            float t = Mathf.Clamp01(Mathf.Log(distance/working)/Mathf.Log(Mathf.Max(1.01f,s.inflationEndRatio)));
            float angle = Mathf.Lerp(s.nearTrailWidth/working,s.farTrailAngleRadians,t*t*(3-2*t));
            return Mathf.Max(s.nearTrailWidth,distance*angle);
        }

        public void Draw(Vector3 logical, Camera view, bool selected, float time, float dt)
        {
            EnsureReady();
            if (style == null) style = new BirdDepthStyle();
            gameObject.SetActive(true);
            Vector3 eye = view.transform.position;
            float d = Mathf.Max(.001f, (logical-eye).magnitude);
            float renderD = RenderDistance(d);
            Vector3 projected = Project(logical, eye);
            float target = TargetDiameter(d, sizeMode, style);
            diameter = AdvanceDiameter(diameter, target, d, dt, sizeMode, style);
            core.position = projected;
            core.localScale = Vector3.one*(diameter*renderD/d);
            coreMaterial.color = selected ? Color.Lerp(tint, Color.white, .65f) : tint;

            // A separate thin locator survives when the honest shrinking core
            // gets subpixel. It is not the solid cursor's physical size.
            float coreAngle = diameter/d;
            float locatorAngle = style.locatorAngleRadians;
            float haloBlend = 1-Mathf.Clamp01((coreAngle-.0015f)/.003f);
            halo.enabled = style.showFarLocator && d > style.workingDistance && haloBlend > 0;
            halo.startColor = halo.endColor = new Color(tint.r,tint.g,tint.b,.55f*haloBlend);
            halo.startWidth = halo.endWidth = renderD*.0008f;
            for (int i = 0; i < 40; i++)
            {
                float a = i*Mathf.PI*2/40;
                halo.SetPosition(i, projected + renderD*locatorAngle*.5f*(view.transform.right*Mathf.Cos(a)+view.transform.up*Mathf.Sin(a)));
            }

            while (count > 0 && time-times[0] > .7f) RemoveOldest();
            if (showTrail)
            {
                if (count == 0 || time-lastSampleTime >= .014f)
                {
                    if (count == Capacity) RemoveOldest();
                    history[count] = logical; times[count++] = time;
                    lastSampleTime = time;
                }
                else
                {
                    // Keep the live tip attached between stored samples. Use
                    // a separate sampling clock so this does not postpone the
                    // next sample indefinitely at high update rates.
                    history[count-1] = logical;
                    times[count-1] = time;
                }
            }
            ribbonRenderer.enabled = showTrail && count > 1;
            for (int i = 0; i < Capacity; i++)
            {
                if (i >= count) { vertices[i*2] = vertices[i*2+1] = count > 0 ? vertices[(count-1)*2] : Vector3.zero; colors[i*2] = colors[i*2+1] = Color.clear; continue; }
                Vector3 p = Project(history[i], eye);
                Vector3 previous = Project(history[Mathf.Max(0,i-1)], eye);
                Vector3 next = Project(history[Mathf.Min(count-1,i+1)], eye);
                Vector3 sideways = Vector3.Cross(next-previous, p-eye).normalized;
                if (sideways.sqrMagnitude < .5f) sideways = view.transform.right;
                float pointD = Mathf.Max(.001f,(history[i]-eye).magnitude);
                float age = Mathf.Clamp01(1-(time-times[i])/.7f);
                // Every historical point owns its width; a far tip never widens
                // the nearby part of the ribbon. Trail widths have no time lag.
                float width = TrailWidth(pointD, style);
                width *= RenderDistance(pointD)/pointD * (.25f+.75f*age);
                vertices[i*2] = transform.InverseTransformPoint(p-sideways*width*.5f);
                vertices[i*2+1] = transform.InverseTransformPoint(p+sideways*width*.5f);
                colors[i*2] = colors[i*2+1] = new Color(tint.r,tint.g,tint.b,age*.65f);
            }
            ribbon.vertices = vertices;
            ribbon.colors = colors;
            ribbon.RecalculateBounds();
        }

        void RemoveOldest() { count--; for (int i=0;i<count;i++) { history[i]=history[i+1]; times[i]=times[i+1]; } }
        public void Clear() { count=0; lastSampleTime=0; diameter=style != null ? style.nearDiameter : NearDiameter; if (ready) { ribbonRenderer.enabled=false; halo.enabled=false; } }
        void OnDestroy() { if (ribbon!=null) Destroy(ribbon); if(coreMaterial!=null)Destroy(coreMaterial); if(lineMaterial!=null)Destroy(lineMaterial); }
    }
}
