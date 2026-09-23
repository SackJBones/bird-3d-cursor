using Bird3DCursor;
using UnityEngine;

// Synthetic sphere samples, not an anatomical hand model or tracking adapter.
public sealed class BirdDesktopPreview : MonoBehaviour
{
    private readonly PoseHand[] hands = new PoseHand[2];
    private readonly Bird[] birds = new Bird[2];
    private readonly Transform[,] joints = new Transform[2, 16];
    private readonly Transform[] cursors = new Transform[2];
    private readonly Transform[] tips = new Transform[2];
    private readonly LineRenderer[] rays = new LineRenderer[2];
    private readonly Material[] materials = new Material[2];
    private Material jointMaterial;
    private Transform visualRoot;
    private bool animate = true, tracking = true, pressed;
    private float radius = 0.035f, phase;
    private int presses, releases;

    private void Awake()
    {
        visualRoot = new GameObject("Generated preview visuals").transform;
        visualRoot.SetParent(transform, false);
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.SetParent(visualRoot, false);
        camera.transform.position = new Vector3(0.9f, 0.65f, -1.1f);
        camera.transform.LookAt(new Vector3(0, 0, 0.25f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.075f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 10;
        jointMaterial = MakeMaterial(new Color(0.55f, 0.62f, 0.73f));
        for (int h = 0; h < 2; h++)
        {
            hands[h] = new PoseHand(h == 0 ? Hand.Chirality.Left : Hand.Chirality.Right);
            birds[h] = new Bird(hands[h]);
            materials[h] = MakeMaterial(h == 0 ? Color.cyan : new Color(1, 0.4f, 0.7f));
            cursors[h] = Sphere("Bird cursor " + h, 0.018f, materials[h]);
            tips[h] = Sphere("Index tip " + h, 0.008f, materials[h]);
            for (int j = 0; j < 16; j++) joints[h, j] = Sphere("Fit point " + h + ":" + j, 0.006f, jointMaterial);
            var lineObject = new GameObject("Root to cursor " + h);
            lineObject.transform.SetParent(visualRoot, false);
            rays[h] = lineObject.AddComponent<LineRenderer>();
            rays[h].sharedMaterial = materials[h];
            rays[h].positionCount = 2;
            rays[h].startWidth = rays[h].endWidth = 0.002f;
        }
    }

    private Material MakeMaterial(Color color)
    {
        return new Material(Shader.Find("Unlit/Color")) { color = color };
    }

    private Transform Sphere(string label, float size, Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = label;
        sphere.transform.SetParent(visualRoot, false);
        sphere.transform.localScale = Vector3.one * size;
        sphere.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(sphere.GetComponent<Collider>());
        return sphere.transform;
    }

    private void Update()
    {
        if (animate) phase += Time.deltaTime;
        for (int h = 0; h < 2; h++)
        {
            var hand = hands[h];
            hand.tracked = tracking;
            hand.pressed = pressed;
            hand.radius = radius;
            hand.center = new Vector3(h == 0 ? -0.16f : 0.16f, 0, 0.05f);
            hand.rotation = Quaternion.Euler(18 * Mathf.Sin(phase), 25 * Mathf.Sin(phase * 0.7f + h), 0);
            birds[h].Update();
            if (birds[h].GetClickDown()) presses++;
            if (birds[h].GetClickUp()) releases++;
            cursors[h].position = birds[h].GetPosition();
            cursors[h].localScale = Vector3.one * (birds[h].GetClick() ? 0.027f : 0.018f);
            tips[h].position = hand.GetTipPosition(Finger.Index);
            tips[h].gameObject.SetActive(tracking);
            for (int j = 0; j < 16; j++)
            {
                joints[h, j].position = hand.Point(j);
                joints[h, j].gameObject.SetActive(tracking);
            }
            rays[h].SetPosition(0, birds[h].GetHandRoot());
            rays[h].SetPosition(1, birds[h].GetPosition());
            rays[h].enabled = tracking;
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 330, 285), GUI.skin.box);
        GUILayout.Label("BIRD / DESKTOP PREVIEW");
        GUILayout.Label("Synthetic sphere points — no hand tracking");
        animate = GUILayout.Toggle(animate, "Animate orientation");
        tracking = GUILayout.Toggle(tracking, "Pose available");
        pressed = GUILayout.Toggle(pressed, "Index inside sphere (hold selection)");
        GUILayout.Label("Sphere radius: " + (radius * 1000).ToString("F0") + " mm");
        radius = GUILayout.HorizontalSlider(radius, 0.02f, 0.045f);
        GUILayout.Label("Cyan: left / Pink: right");
        if (birds[0] != null)
        {
            GUILayout.Label("Left: " + (birds[0].GetClick() ? "SELECTED" : "released") +
                "    range " + birds[0].GetRange().ToString("F2") + " m");
            GUILayout.Label("Press edges: " + presses + " / Release edges: " + releases);
        }
        GUILayout.Label("Hide the pose while selected to test release.");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
        foreach (var material in materials) if (material != null) Destroy(material);
        if (jointMaterial != null) Destroy(jointMaterial);
    }

    private sealed class PoseHand : Hand
    {
        public bool tracked, pressed;
        public float radius;
        public Vector3 center;
        public Quaternion rotation;
        public PoseHand(Chirality side) : base(side) { }
        public override bool IsTracking() { return tracked; }
        public Vector3 Point(int index)
        {
            float z = 1 - 2 * (index + 0.5f) / 16;
            float radial = Mathf.Sqrt(1 - z * z);
            float angle = index * 2.39996323f;
            return center + rotation * (radius * new Vector3(radial * Mathf.Cos(angle), radial * Mathf.Sin(angle), z));
        }
        public override Vector3 GetBasePosition(Finger finger)
        {
            if (finger == Finger.Thumb)
                return (center - rotation * Vector3.forward * radius - 0.6f * Point(3)) / 0.4f;
            return finger == Finger.Index ? Point(3) : Point(4 + ((int)finger - 2) * 4);
        }
        public override Vector3 GetIntermediatePosition(Finger finger) { return Joint(finger, 1); }
        public override Vector3 GetDistalPosition(Finger finger) { return Joint(finger, 2); }
        public override Vector3 GetTipPosition(Finger finger)
        {
            return finger == Finger.Index ? center + rotation * Vector3.right * (pressed ? 0 : radius * 3) : Joint(finger, 3);
        }
        private Vector3 Joint(Finger finger, int joint)
        {
            if (finger == Finger.Thumb) return Point(joint - 1);
            if (finger == Finger.Index) return Vector3.Lerp(Point(3), GetTipPosition(finger), joint / 3f);
            return Point(4 + ((int)finger - 2) * 4 + joint);
        }
    }
}
