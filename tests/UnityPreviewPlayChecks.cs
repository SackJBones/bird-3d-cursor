using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Bird3DCursor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// Generated-project-only integration checks. Never shipped with the sample or package.
public sealed class UnityPreviewPlayChecks : MonoBehaviour
{
    private int checks;
    private string runtimeError;
    private BirdDesktopPreview preview;
    private bool renderPreview;

    private void OnEnable() { Application.logMessageReceived += CaptureError; }
    private void OnDisable() { Application.logMessageReceived -= CaptureError; }
    private void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            runtimeError = message + "\n" + stack;
    }

    private IEnumerator Start()
    {
        renderPreview = Array.IndexOf(Environment.GetCommandLineArgs(), "-birdRenderPreview") >= 0;
        var run = RunChecks();
        while (true)
        {
            bool next;
            try
            {
                if (runtimeError != null) throw new Exception(runtimeError);
                next = run.MoveNext();
            }
            catch (Exception e)
            {
                File.WriteAllText("core-checks-result.txt", "FAIL: preview Play Mode: " + e);
                EditorApplication.Exit(1);
                yield break;
            }
            if (!next) break;
            yield return run.Current;
        }
        File.WriteAllText("core-checks-result.txt", "PASS: " + checks +
            " desktop preview Play Mode checks; Unity " + Application.unityVersion +
            (renderPreview ? "; camera rendering checked; no GUI input or hardware validation" : "; no rendering or hardware validation"));
        EditorApplication.Exit(0);
    }

    private T Read<T>(string field)
    {
        return (T)typeof(BirdDesktopPreview).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
    }
    private void Set(string field, object value)
    {
        typeof(BirdDesktopPreview).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(preview, value);
    }
    private void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private IEnumerator RunChecks()
    {
        preview = FindObjectOfType<BirdDesktopPreview>();
        Check(preview != null, "Sample must enter Play Mode");
        Set("animate", false);
        for (int i = 0; i < 30; i++) yield return null;
        var birds = Read<Bird[]>("birds");
        var cursors = Read<Transform[]>("cursors");
        var joints = Read<Transform[,]>("joints");
        var rays = Read<LineRenderer[]>("rays");
        var tips = Read<Transform[]>("tips");
        var camera = preview.GetComponentInChildren<Camera>();
        Check(camera != null, "Sample must create its camera");
        Check(preview.GetComponentsInChildren<Collider>().Length == 0, "Visual markers must not retain physics colliders");
        float nearRange = birds[0].GetRange();
        for (int h = 0; h < 2; h++)
        {
            Check(Mathf.Abs(birds[h].GetSphereFitRadius() - 0.035f) < 0.0001f, "Known synthetic sphere must be fitted");
            Check(!birds[h].GetClick(), "Preview must start released");
            Check(Vector3.Distance(cursors[h].position, birds[h].GetPosition()) < 0.00001f, "Visual cursor must follow solver");
            Check(Vector3.Distance(rays[h].GetPosition(1), cursors[h].position) < 0.00001f, "Ray must end at visual cursor");
        }
        Check(Vector3.Distance(cursors[0].position, cursors[1].position) > 0.1f, "Two hands must produce distinct positions");
        if (renderPreview) CaptureCamera(camera, cursors, "preview-idle.png");
        Set("radius", 0.045f);
        for (int i = 0; i < 120; i++) yield return null;
        Check(birds[0].GetRange() > nearRange + 0.08f, "Opening synthetic hand must extend cursor range");
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        Check(birds[0].GetClick() && birds[1].GetClick(), "Index penetration must select both cursors");
        Check(Read<int>("presses") == 2, "Held selection must produce only one press per hand");
        Check(cursors[0].localScale.x > 0.02f, "Selected cursor must enlarge");
        if (renderPreview) CaptureCamera(camera, cursors, "preview-selected.png");
        Vector3 leftPosition = cursors[0].position, rightPosition = cursors[1].position;
        Set("tracking", false);
        for (int i = 0; i < 4; i++) yield return null;
        Check(!birds[0].GetClick() && !birds[1].GetClick(), "Pose loss must release both hands");
        Check(Read<int>("releases") == 2, "Continued pose loss must release exactly once per hand");
        Check(cursors[0].position == leftPosition && cursors[1].position == rightPosition, "Pose loss must hold both visual cursors");
        if (renderPreview) CaptureCamera(camera, cursors, "preview-lost-pose.png");
        for (int h = 0; h < 2; h++)
        {
            Check(!rays[h].enabled && !tips[h].gameObject.activeSelf, "Pose loss must hide ray and index marker");
            for (int j = 0; j < 16; j++) Check(!joints[h, j].gameObject.activeSelf, "Pose loss must hide every fitting point");
        }
        Set("pressed", false);
        Set("tracking", true);
        for (int i = 0; i < 4; i++) yield return null;
        Check(Read<int>("presses") == 2 && Read<int>("releases") == 2, "Unpressed recovery must not add click edges");
        Check(rays[0].enabled && tips[0].gameObject.activeSelf && joints[1, 15].gameObject.activeSelf, "Recovery must restore input visuals");
        CheckTrailBudget();
        var trails = Read<BirdTrail[]>("trails");
        if (renderPreview)
        {
            for (int i = 0; i < 80; i++)
            {
                Set("phase", i * 0.045f);
                yield return new WaitForSeconds(0.025f);
            }
            Check(trails[0].Count > 4 && trails[1].Count > 4, "Animated preview must accumulate trails for both hands");
            CaptureCamera(camera, cursors, "preview-trails.png");
        }
        Set("tracking", false);
        for (int i = 0; i < 3; i++) yield return null;
        Check(trails[0].Count == 0 && trails[1].Count == 0, "Preview tracking loss must clear both trails");
        Set("tracking", true);
        Set("pressed", false);
        for (int i = 0; i < 3; i++) yield return null;
        // Bridge the real synthetic Bird into a disabled provider; no SDK or prefab startup is exercised.
        var providerObject = new GameObject("Synthetic interaction provider");
        var provider = providerObject.AddComponent<BirdProvider>();
        provider.enabled = false;
        typeof(BirdProvider).GetField("bird", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(provider, birds[0]);
        var target = new GameObject("Event-only drag target");
        target.SetActive(false);
        target.AddComponent<BoxCollider>().size = Vector3.one * 5;
        var interactable = target.AddComponent<BirdInteractable>();
        interactable.bird = provider;
        interactable.selectBehind = false;
        interactable.motionType = BirdInteractable.MotionType.None;
        interactable.activateType = BirdInteractable.ActivateType.Drag;
        interactable.snapObjects = new GameObject[0];
        int selections = 0, deselections = 0;
        var onSelect = new UnityEvent();
        var onDeselect = new UnityEvent();
        onSelect.AddListener(() => selections++);
        onDeselect.AddListener(() => deselections++);
        typeof(BirdInteractable).GetField("OnSelect", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(interactable, onSelect);
        typeof(BirdInteractable).GetField("OnDeselect", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(interactable, onDeselect);
        target.SetActive(true);
        for (int i = 0; i < 4; i++) yield return null;
        Check(selections == 0 && deselections == 0, "Idle interactable must not emit deselection");
        Set("pressed", true);
        for (int i = 0; i < 4; i++) yield return null;
        Check(selections == 1 && deselections == 0, "Held event-only drag must select once");
        Set("pressed", false);
        for (int i = 0; i < 4; i++) yield return null;
        Check(selections == 1 && deselections == 1, "Drag release must deselect once, including MotionType.None");
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        Set("tracking", false);
        for (int i = 0; i < 4; i++) yield return null;
        Check(selections == 2 && deselections == 2, "Tracking loss must deselect once");
        Set("tracking", true);
        Set("pressed", false);
        for (int i = 0; i < 3; i++) yield return null;
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        interactable.enabled = false;
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 3 && deselections == 3, "Disabling a selected interactable must deselect once");
        interactable.enabled = true;
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 3 && deselections == 3, "Re-enable while held must not fabricate a fresh press");
        Set("pressed", false);
        for (int i = 0; i < 3; i++) yield return null;
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        interactable.bird = null;
        for (int i = 0; i < 4; i++) yield return null;
        Check(selections == 4 && deselections == 4, "Losing the provider must deselect once");
        interactable.bird = provider;
        interactable.activateType = BirdInteractable.ActivateType.Toggle;
        Set("pressed", false);
        for (int i = 0; i < 3; i++) yield return null;
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        Set("pressed", false);
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 5 && deselections == 4, "Event-only toggle must remain selected after release");
        Set("pressed", true);
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 5 && deselections == 5, "Second toggle press must deselect once");
        interactable.enabled = false;
        interactable.activateType = BirdInteractable.ActivateType.Touch;
        target.transform.position = Vector3.one * 100;
        Physics.SyncTransforms();
        interactable.enabled = true;
        for (int i = 0; i < 3; i++) yield return null;
        target.transform.position = Vector3.zero;
        Physics.SyncTransforms();
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 6 && deselections == 5, "Touch entry must select an event-only target once");
        interactable.enabled = false;
        interactable.enabled = true;
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 7 && deselections == 6, "Touch re-enable must begin a new hover selection");
        target.transform.position = Vector3.one * 100;
        Physics.SyncTransforms();
        for (int i = 0; i < 3; i++) yield return null;
        Check(selections == 7 && deselections == 7, "Touch exit must deselect once");
        Destroy(target);
        Destroy(providerObject);
        var materials = Read<Material[]>("materials");
        var jointMaterial = Read<Material>("jointMaterial");
        var trailMaterial = Read<Material>("trailMaterial");
        var owner = preview.gameObject;
        var authoredChild = new GameObject("Unrelated authored child");
        authoredChild.transform.SetParent(owner.transform);
        Destroy(preview);
        for (int i = 0; i < 3; i++) yield return null;
        Check(camera == null && owner.GetComponentsInChildren<Renderer>().Length == 0,
            "Removing the preview component must remove its generated camera and visuals");
        Check(materials[0] == null && materials[1] == null && jointMaterial == null, "Removing preview must release generated materials");
        Check(trailMaterial == null, "Removing preview must release trail material");
        Check(authoredChild != null && owner.transform.childCount == 1, "Cleanup must preserve unrelated authored children");
    }

    private void CheckTrailBudget()
    {
        var owner = new GameObject("Trail budget check");
        var line = owner.AddComponent<LineRenderer>();
        try
        {
            var trail = new BirdTrail(line, Color.cyan, 3, 2, 0, 0.01f);
            for (int i = 0; i <= 10; i++) trail.Update(Vector3.right * i, true, i * 0.1f);
            Check(trail.Count == 3 && line.positionCount == 3, "Trail must obey point budget after ring wrap");
            Check(line.GetPosition(0).x == 8 && line.GetPosition(2).x == 10, "Budget eviction must preserve chronological newest points");
            trail.Update(Vector3.right * 10, true, 1.4f);
            Check(trail.Count == 3, "Stationary samples must not grow the trail");
            Check(line.startColor.a < line.endColor.a && line.endColor.a < 1, "Stationary trail must fade with age");
            trail.Update(Vector3.right * 10, true, 4);
            Check(trail.Count == 1, "Expired trail must leave only one fresh, non-drawing anchor");
            trail.Update(Vector3.one, false, 4.1f);
            Check(trail.Count == 0 && line.positionCount == 0, "Tracking loss must clear trail geometry");
            trail.Update(Vector3.one * 20, true, 4.2f);
            Check(trail.Count == 1 && line.GetPosition(0) == Vector3.one * 20, "Recovery must not connect to pre-loss position");
            trail.Update(new Vector3(float.NaN, 0, 0), true, 4.3f);
            Check(trail.Count == 0, "Nonfinite positions must break the trail");
            trail.Update(Vector3.one, true, 5);
            trail.Update(Vector3.zero, true, 0);
            Check(trail.Count == 1 && line.GetPosition(0) == Vector3.zero, "Clock rewind must start a fresh trail");
            trail.Clear();
            Check(trail.Count == 0 && line.positionCount == 0, "Explicit clear must remove geometry");
            trail = new BirdTrail(line, Color.white, 8, 2, 0.2f, 0.1f);
            trail.Update(Vector3.zero, true, 0);
            trail.Update(Vector3.right, true, 0.1f);
            Check(trail.Count == 1, "Sample interval must reject too-frequent points");
            trail.Update(Vector3.right * 0.01f, true, 0.3f);
            Check(trail.Count == 1, "Distance threshold must reject tiny movements");
            trail.Update(Vector3.right, true, 0.4f);
            Check(trail.Count == 2, "Valid motion after thresholds must produce a segment");
        }
        finally { Destroy(owner); }
    }

    private void CaptureCamera(Camera camera, Transform[] cursors, string fileName)
    {
        Check(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
            "Render validation requires a real graphics device");
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1280f / 720f;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(fileName, pixels.EncodeToPNG());
            for (int h = 0; h < 2; h++)
            {
                Vector3 position = camera.WorldToViewportPoint(cursors[h].position);
                Check(position.z > 0 && position.x > 0.02f && position.x < 0.98f &&
                    position.y > 0.02f && position.y < 0.98f, "Cursor must be inside the camera frame: " + fileName);
                int count = 0;
                int cx = Mathf.RoundToInt(position.x * 1280), cy = Mathf.RoundToInt(position.y * 720);
                for (int y = cy - 4; y <= cy + 4; y++)
                    for (int x = cx - 4; x <= cx + 4; x++)
                    {
                        Color color = pixels.GetPixel(x, y);
                        bool expected = h == 0 ? color.r < 0.2f && color.g > 0.7f && color.b > 0.7f :
                            color.r > 0.7f && color.g > 0.2f && color.g < 0.7f && color.b > 0.4f;
                        if (expected) count++;
                    }
                Check(count >= 3, "Expected cursor color must render near its projected position: " + fileName + " hand=" + h);
            }
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            target.Release();
            Destroy(target);
            Destroy(pixels);
        }
    }
}
