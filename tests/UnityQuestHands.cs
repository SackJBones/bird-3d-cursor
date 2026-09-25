#if BIRD_OPENXR_ENABLED
using System;
using System.Collections.Generic;
using Bird3DCursor;
using Bird3DCursor.Presentation;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using Hand = Bird3DCursor.Hand;

// Standalone, controller-free physical comparison. All math and visuals use the
// same unscaled tracking space. No neutral calibration or range multiplier.
public sealed class UnityQuestHands : MonoBehaviour
{
    sealed class CapturedHand : Hand
    {
        readonly Hand source;
        public readonly Vector3[] joints = new Vector3[20];
        public bool valid;
        public int available;
        public CapturedHand(Chirality side) : base(side) { source = new OpenXRHand(side); }
        public void Capture()
        {
            valid = source.IsTracking();
            available = 0;
            for (int f = 0; f < 5; f++)
            {
                var finger = (Finger)f;
                int i = f * 4;
                joints[i] = source.GetBasePosition(finger);
                joints[i + 1] = source.GetIntermediatePosition(finger);
                joints[i + 2] = source.GetDistalPosition(finger);
                joints[i + 3] = source.GetTipPosition(finger);
                for (int j = 0; j < 4; j++)
                {
                    if (f == 1 && (j == 1 || j == 2)) continue; // visual-only index joints
                    // The existing adapter reports an unavailable pose as zero.
                    bool present = Finite(joints[i + j]) && joints[i + j] != Vector3.zero;
                    if (present) available++;
                    else valid = false;
                }
            }
        }
        public override bool IsTracking() { return valid; }
        public override Vector3 GetBasePosition(Finger f) { return joints[(int)f * 4]; }
        public override Vector3 GetIntermediatePosition(Finger f) { return joints[(int)f * 4 + 1]; }
        public override Vector3 GetDistalPosition(Finger f) { return joints[(int)f * 4 + 2]; }
        public override Vector3 GetTipPosition(Finger f) { return joints[(int)f * 4 + 3]; }
    }

    sealed class Side
    {
        public string name;
        public CapturedHand hand;
        public Bird bird;
        public BirdCursorState port;
        public BirdDepthVisual depthVisual;
        public LineRenderer palmLine;
        public GameObject visual;
        public Transform[] joints = new Transform[20];
        public LineRenderer[] bones = new LineRenderer[5];
        public LineRenderer[] sphere = new LineRenderer[3];
        public LineRenderer portRing;
        public Transform raw, center, root;
        public Vector3[] points = new Vector3[16];
        public Vector3 rawPosition;
        public float centerGap, rawGap, filterGap, radius, distance, range;
        public bool visible;
        public int frames, accepted;
    }

    static readonly int[] FitIndices = { 1, 2, 3, 4, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
    readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    XRHandSubsystem subsystem;
    Side[] sides;
    Camera view;
    TextMesh label;
    float nextStatus;
    bool subscribed;
    int dynamicSamples;
    Material lineMaterial;
    TextMesh[] modeLabels;
    int touchMode = -1;
    float touchSince, nextModeChange;
    BirdDepthVisual.SizeMode sizeMode = BirdDepthVisual.SizeMode.InflationWithLag;

    void Start()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        view = new GameObject("Tracking-space camera").AddComponent<Camera>();
        view.tag = "MainCamera";
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(.012f, .018f, .035f);
        view.nearClipPlane = .005f;
        view.farClipPlane = 1000;
        view.stereoTargetEye = StereoTargetEyeMask.Both;
        label = new GameObject("Instructions and diagnostics").AddComponent<TextMesh>();
        label.transform.SetParent(view.transform, false);
        label.transform.localPosition = new Vector3(-.48f, .38f, 1.35f);
        label.fontSize = 48;
        label.characterSize = .014f;
        label.anchor = TextAnchor.UpperLeft;
        label.color = new Color(.85f, .9f, 1);
        label.text = "BIRD / LIVE HANDS\nStarting OpenXR hand tracking...";
        sides = new[] { CreateSide(Hand.Chirality.Left, new Color(.1f, .9f, 1)),
            CreateSide(Hand.Chirality.Right, new Color(1, .25f, .65f)) };
        CreateModeControls();
        Application.onBeforeRender += UpdateHead;
        Debug.Log("BIRD_HANDS_START: v0.3 palm continuation + attached depth trail; original Bird.cs reference; real XR Hands; 32mm cursor through 4m; Q=.001 R=270*d^3");
    }

    Side CreateSide(Hand.Chirality chirality, Color color)
    {
        var side = new Side { name = chirality.ToString(), hand = new CapturedHand(chirality) };
        side.bird = new Bird(side.hand);
        var math = new GameObject(side.name + " port math comparison");
        side.port = math.AddComponent<BirdCursorState>();
        side.port.fitter = math.AddComponent<BirdSphereFit>();
        side.port.smoothing = true;
        side.port.fitter.constrainToPalm = true;
        side.port.points = side.points;
        side.visual = new GameObject(side.name + " diagnostics");
        side.depthVisual = new GameObject(side.name + " depth cursor").AddComponent<BirdDepthVisual>();
        side.depthVisual.transform.SetParent(side.visual.transform, false);
        side.depthVisual.tint = color;
        side.palmLine = Line(side.visual.transform, Color.green, .0015f, 2, false);
        for (int i = 0; i < 20; i++) side.joints[i] = Dot(side.visual.transform, i == 7 ? Color.white : color, .004f);
        for (int i = 0; i < 5; i++) side.bones[i] = Line(side.visual.transform, color * .7f, .0015f, 4, false);
        for (int i = 0; i < 3; i++) side.sphere[i] = Line(side.visual.transform, color, .001f, 48, true);
        side.raw = Dot(side.visual.transform, Color.white, .005f);
        side.center = Dot(side.visual.transform, color, .004f);
        side.root = Dot(side.visual.transform, Color.green, .004f);
        side.portRing = Line(side.visual.transform, new Color(1, .7f, .1f), .002f, 32, true);
        side.visual.SetActive(false);
        return side;
    }

    Transform Dot(Transform parent, Color color, float diameter)
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(dot.GetComponent<Collider>());
        dot.transform.SetParent(parent, false);
        dot.transform.localScale = Vector3.one * diameter;
        var material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        dot.GetComponent<Renderer>().sharedMaterial = material;
        return dot.transform;
    }

    LineRenderer Line(Transform parent, Color color, float width, int count, bool loop)
    {
        var line = new GameObject("Diagnostic line").AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.sharedMaterial = lineMaterial;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.positionCount = count;
        line.loop = loop;
        line.useWorldSpace = true;
        return line;
    }

    void Update()
    {
        UpdateHead();
        if (subsystem == null || !subsystem.running)
        {
            Unsubscribe();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var candidate in subsystems)
                if (candidate.running) { subsystem = candidate; break; }
            if (subsystem != null && subsystem.running)
            {
                subsystem.updatedHands += OnHands;
                subscribed = true;
                Debug.Log("BIRD_HANDS_SUBSYSTEM: running " + subsystem.subsystemDescriptor.id);
            }
            else foreach (var side in sides) Lose(side);
        }
        if (Time.unscaledTime >= nextStatus)
        {
            nextStatus = Time.unscaledTime + 1;
            string status = "BIRD / PALM + DEPTH TEST   |   cup, flare, return\n" +
                "Color + sphere: guarded Bird  |  white: raw  |  gold: legacy\n" +
                "32mm through 4m. Green line points out of palm.\n" +
                "Touch a mode label below for 0.6s: " + sizeMode + "\n";
            foreach (var side in sides) status += Describe(side) + "\n";
            label.text = status;
            Debug.Log("BIRD_HANDS_STATUS: xr=" + XRSettings.isDeviceActive + " subsystem=" +
                (subsystem != null && subsystem.running) + " samples=" + dynamicSamples + " " + Describe(sides[0]) + " | " + Describe(sides[1]));
        }
    }

    void UpdateHead()
    {
        if (view == null) return;
        view.transform.SetPositionAndRotation(InputTracking.GetLocalPosition(XRNode.Head), InputTracking.GetLocalRotation(XRNode.Head));
    }

    void OnHands(XRHandSubsystem source, XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType type)
    {
        // Kalman is sample-dependent: never update it again for BeforeRender.
        if (type != XRHandSubsystem.UpdateType.Dynamic) return;
        dynamicSamples++;
        for (int i = 0; i < sides.Length; i++)
        {
            var side = sides[i];
            var needed = i == 0 ? XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints : XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
            side.hand.Capture();
            side.hand.valid &= (flags & needed) != 0;
            side.frames++;
            side.bird.Update();
            if (!side.hand.valid) { Lose(side); continue; }
            for (int j = 0; j < 16; j++) side.points[j] = side.hand.joints[FitIndices[j]];
            side.port.handRoot = .6f * side.hand.joints[4] + .4f * side.hand.joints[0];
            Vector3 normal = Vector3.Cross(side.hand.joints[4]-side.hand.joints[0], side.hand.joints[16]-side.hand.joints[0]);
            normal *= i == 0 ? -1 : 1;
            // OpenXR palm +Y points out of the back of the hand. Use the
            // tracked orientation to disambiguate winding when it is available.
            Pose palmPose;
            var xrHand = i == 0 ? source.leftHand : source.rightHand;
            if (xrHand.GetJoint(XRHandJointID.Palm).TryGetPose(out palmPose) && Vector3.Dot(normal, palmPose.rotation*Vector3.down) < 0) normal = -normal;
            side.port.fitter.palmOrigin = side.port.handRoot;
            side.port.fitter.palmNormal = normal;
            side.port.indexTip = side.hand.joints[7];
            side.port.tracking = true;
            side.port.Step();
            side.distance = (side.port.fitter.center - side.port.handRoot).magnitude;
            side.radius = side.port.fitter.radius;
            side.range = Range(side.distance);
            side.rawPosition = side.port.rawPosition;
            side.centerGap = Vector3.Distance(side.bird.GetSphereFitCenter(), side.port.fitter.center);
            float legacyD = (side.bird.GetSphereFitCenter()-side.bird.GetHandRoot()).magnitude;
            side.rawGap = Vector3.Distance(side.rawPosition, side.bird.GetHandRoot() + (side.bird.GetSphereFitCenter()-side.bird.GetHandRoot()).normalized*Range(legacyD));
            side.filterGap = Vector3.Distance(side.bird.GetPosition(), side.port.position);
            // The guarded fit remains usable through the legacy singularity.
            if (!side.port.poseValid || !Finite(side.rawPosition) || side.radius <= 0) { Lose(side); continue; }
            side.accepted++;
            Draw(side);
        }
    }

    void Draw(Side side)
    {
        if (!side.visible) side.depthVisual.Clear();
        side.visible = true;
        side.visual.SetActive(true);
        for (int i = 0; i < 20; i++) side.joints[i].position = side.hand.joints[i];
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 4; j++) side.bones[f].SetPosition(j, side.hand.joints[f * 4 + j]);
        side.center.position = side.port.fitter.center;
        side.root.position = side.port.handRoot;
        side.palmLine.SetPosition(0, side.port.handRoot);
        side.palmLine.SetPosition(1, side.port.handRoot + side.port.fitter.palmNormal.normalized*.06f);
        side.depthVisual.sizeMode = sizeMode;
        side.depthVisual.Draw(side.port.position, view, side.port.selected, Time.unscaledTime, Time.unscaledDeltaTime);
        float rawD = Mathf.Max(.001f,(side.rawPosition-view.transform.position).magnitude);
        side.raw.position = BirdDepthVisual.Project(side.rawPosition,view.transform.position);
        side.raw.localScale = Vector3.one*Mathf.Max(.005f,rawD*.0012f)*BirdDepthVisual.RenderDistance(rawD)/rawD;
        bool legacyVisible = Finite(side.bird.GetPosition());
        side.portRing.gameObject.SetActive(legacyVisible);
        Vector3 legacyDisplay = legacyVisible ? BirdDepthVisual.Project(side.bird.GetPosition(), view.transform.position) : Vector3.zero;
        float legacyRenderD = (legacyDisplay-view.transform.position).magnitude;
        side.portRing.startWidth = side.portRing.endWidth = Mathf.Max(.001f,legacyRenderD*.00025f);
        for (int plane = 0; plane < 3; plane++)
        {
            side.sphere[plane].gameObject.SetActive(true);
            for (int j = 0; j < 48; j++)
            {
                float angle = j * Mathf.PI * 2 / 48;
                float a = Mathf.Cos(angle) * side.radius, b = Mathf.Sin(angle) * side.radius;
                var offset = plane == 0 ? new Vector3(a, b, 0) : plane == 1 ? new Vector3(a, 0, b) : new Vector3(0, a, b);
                side.sphere[plane].SetPosition(j, side.center.position + offset);
            }
        }
        for (int j = 0; j < 32; j++)
        {
            float angle = j * Mathf.PI * 2 / 32;
            side.portRing.SetPosition(j, legacyDisplay + Mathf.Max(.013f,legacyRenderD*.0018f) *
                (view.transform.right * Mathf.Cos(angle) + view.transform.up * Mathf.Sin(angle)));
        }
    }

    void Lose(Side side)
    {
        side.hand.valid = false;
        side.port.Cancel();
        side.visual.SetActive(false);
        side.visible = false;
        side.depthVisual.Clear();
    }

    string Describe(Side s)
    {
        if (!s.visible) return s.name + ": waiting for hand (" + s.hand.available + "/18 joints)";
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}: fit {1:F1}mm  d {2:F1}mm  range {3:G4}m  click {4}/{5}\n    legacy gap: fit {6:G3}mm  raw {7:G3}mm  filtered {8:G3}mm{9}",
            s.name, s.radius * 1000, s.distance * 1000, s.range, s.bird.GetClick() ? 1 : 0, s.port.selected ? 1 : 0,
            s.centerGap * 1000, s.rawGap * 1000, s.filterGap * 1000,
            "  blend=" + s.port.fitter.continuationWeight.ToString("F2"));
    }

    void CreateModeControls()
    {
        modeLabels = new TextMesh[3];
        string[] names = { "Fixed", "Inflate", "Inflate + lag" };
        for (int i=0;i<3;i++)
        {
            var text = new GameObject("Visual mode " + names[i]).AddComponent<TextMesh>();
            text.transform.SetParent(view.transform,false);
            text.transform.localPosition = new Vector3((i-1)*.19f,-.20f,.55f);
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 48;
            text.characterSize = .008f;
            text.text = names[i];
            modeLabels[i] = text;
        }
    }

    void LateUpdate()
    {
        if (modeLabels == null) return;
        int touching = -1;
        for (int i=0;i<3;i++)
        {
            modeLabels[i].color = (int)sizeMode == i ? Color.cyan : Color.gray;
            foreach (var side in sides)
                if (side.hand.valid && (side.hand.joints[7]-modeLabels[i].transform.position).magnitude < .045f) touching=i;
        }
        if (touching != touchMode) { touchMode=touching; touchSince=Time.unscaledTime; }
        if (touching >= 0 && Time.unscaledTime-touchSince > .6f && Time.unscaledTime > nextModeChange)
        {
            sizeMode=(BirdDepthVisual.SizeMode)touching;
            nextModeChange=Time.unscaledTime+1;
            foreach (var side in sides) side.depthVisual.Clear();
            Debug.Log("BIRD_VISUAL_MODE: " + sizeMode);
        }
    }

    public static float Range(float d)
    {
        float near = d / .02f, far = d / .03f;
        return (near + near * near + far * far * far * far * far * far) * .02f;
    }
    static bool Finite(Vector3 p) { return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)); }
    void Unsubscribe()
    {
        if (subsystem != null && subscribed) subsystem.updatedHands -= OnHands;
        subscribed = false;
        subsystem = null;
    }
    void OnDestroy() { Unsubscribe(); Application.onBeforeRender -= UpdateHead; }
}
#endif
