#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Bird3DCursor.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real renderer/mesh checks over deterministic time, not a replacement renderer.
public sealed class UnityDepthMotionChecks : MonoBehaviour
{
    const string Active = "Bird.DepthMotion.Checks";
    const string Folder = "DepthMotionCaptures";
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    int frames, mixedFrames, checks, captures;
    float maxNearDiameter, maxNearTrailWidth, maxRenderRadius;
    float minLagDiameter = float.PositiveInfinity, maxLagDiameter;

    public static void Run()
    {
        File.WriteAllText("depth-motion-result.txt", "PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        if (SessionState.GetBool(Active, false))
            new GameObject("Depth motion checks").AddComponent<UnityDepthMotionChecks>();
    }

    void Start()
    {
        if (!SessionState.GetBool(Active, false)) return;
        try
        {
            Directory.CreateDirectory(Folder);
            string mathResult = UnityDepthVisualChecks.CheckMath();
            var camera = new GameObject("Motion camera").AddComponent<Camera>();
            camera.nearClipPlane = .005f;
            camera.farClipPlane = 1000;
            camera.fieldOfView = 45;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.012f, .018f, .035f);
            var texture = new RenderTexture(1024, 1024, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            var visual = new GameObject("Motion cursor").AddComponent<BirdDepthVisual>();
            var csv = new StringBuilder("mode,fps,time,logical_m,diameter_m,angular_diameter_rad,near_trail_pairs,far_trail_pairs\n");
            foreach (BirdDepthVisual.SizeMode mode in Enum.GetValues(typeof(BirdDepthVisual.SizeMode)))
            foreach (int fps in new[] { 30, 72, 120 })
            {
                visual.sizeMode = mode;
                visual.Clear();
                int maxActivePairs = 0;
                // Translating the eye also exercises historical-point reprojection.
                for (int frame = 0; frame <= fps * 2; frame++)
                {
                    float time = (float)frame / fps;
                    camera.transform.position = new Vector3(.04f * Mathf.Sin(time * 3), 0, 0);
                    float distance = DistanceAt(time);
                    Vector3 direction = new Vector3(.11f * Mathf.Sin(time * 5), .08f * Mathf.Cos(time * 4), 1).normalized;
                    Vector3 logical = camera.transform.position + direction * distance;
                    bool selected = (frame / 7) % 2 == 0;
                    visual.Draw(logical, camera, selected, time, 1f / fps);
                    if (mode == BirdDepthVisual.SizeMode.InflationWithLag && frame * 2 == fps)
                    {
                        minLagDiameter = Mathf.Min(minLagDiameter, visual.CurrentDiameter);
                        maxLagDiameter = Mathf.Max(maxLagDiameter, visual.CurrentDiameter);
                    }
                    Transform core = visual.transform.Find("Sphere");
                    Require(core != null, "Missing real cursor mesh");
                    float physicalDiameter = core.lossyScale.x;
                    Require(Finite(core.position) && Finite(core.localScale), "Nonfinite rendered cursor");
                    Require((core.position - camera.transform.position).magnitude <= 500.01f, "Cursor left render shell");
                    if (distance <= 4)
                    {
                        maxNearDiameter = Mathf.Max(maxNearDiameter, physicalDiameter);
                        Require(Mathf.Abs(physicalDiameter - .032f) < .000001f, "Oversize near cursor after return/selection");
                    }
                    var filter = visual.transform.Find("Depth-aware ribbon").GetComponent<MeshFilter>();
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    Color[] colors = filter.sharedMesh.colors;
                    int near = 0, far = 0;
                    int activePairs = 0;
                    Vector3 trailTip = Vector3.zero;
                    Require(vertices.Length == 192, "Unbounded trail buffer");
                    for (int i = 0; i < vertices.Length; i += 2)
                    {
                        Vector3 a = filter.transform.TransformPoint(vertices[i]);
                        Vector3 b = filter.transform.TransformPoint(vertices[i + 1]);
                        Require(Finite(a) && Finite(b), "Nonfinite historical trail geometry");
                        if (colors[i].a <= .00001f) continue;
                        activePairs++;
                        Vector3 center = (a + b) * .5f;
                        trailTip = center;
                        float radius = (center - camera.transform.position).magnitude;
                        maxRenderRadius = Mathf.Max(maxRenderRadius, radius);
                        Require(radius <= 500.01f, "Trail left render shell");
                        if (radius <= 4)
                        {
                            near++;
                            float width = Vector3.Distance(a, b);
                            maxNearTrailWidth = Mathf.Max(maxNearTrailWidth, width);
                            Require(width <= .002001f, "Far history widened a near trail point");
                        }
                        if (radius >= 50) far++;
                    }
                    Require(Vector3.Distance(trailTip, core.position) < .0003f,
                        "Rendered trail tip detached from current cursor at " + fps + "Hz frame " + frame);
                    maxActivePairs = Mathf.Max(maxActivePairs, activePairs);
                    if (near > 0 && far > 0) mixedFrames++;
                    csv.AppendFormat(Invariant, "{0},{1},{2:F6},{3:G9},{4:G9},{5:G9},{6},{7}\n",
                        mode, fps, time, distance, visual.CurrentDiameter, visual.CurrentDiameter / distance, near, far);
                    frames++;
                    // Six representative states of a fast stroke, captured at 72 Hz.
                    if (fps == 72 && (frame == 18 || frame == 43 || frame == 70 || frame == 78 || frame == 81 || frame == 120))
                        Capture(camera, texture, mode + "-frame-" + frame);
                }
                Require(maxActivePairs >= 8, "History sampling stopped at " + fps + "Hz");
                // Loss/recovery must not reconnect a new near stroke to far history.
                visual.Draw(new Vector3(0, 0, 1e9f), camera, false, 3, 1f / fps);
                visual.Clear();
                visual.Draw(camera.transform.position + Vector3.forward * .4f, camera, true, 4, 1f / fps);
                Require(!visual.transform.Find("Depth-aware ribbon").GetComponent<Renderer>().enabled,
                    "Recovery connected to stale trail history");
                Require(visual.CurrentDiameter == .032f, "Recovery retained distant size");
            }
            Require(mixedFrames > 0, "Fixture did not exercise mixed-depth history");
            File.WriteAllText(Folder + "/motion.csv", csv.ToString());
            float lagSpread = (maxLagDiameter-minLagDiameter)/minLagDiameter;
            Require(lagSpread < .02f, "Outward lag varies by " + (lagSpread*100).ToString("F3",Invariant) + "% across rates at the same stroke time");
            string result = string.Format(Invariant,
                "PASS: {0} real renderer updates at simulated 30/72/120Hz across 3 modes; {1} assertions; {2} mixed near/far trail updates; max near diameter={3:F6}m, near trail width={4:F6}m, render radius={5:F3}m; {6} Unity captures; recovery/selection checked; outbound diameter spread={7:F3}%. No hardware frame-pacing, subjective perception or stereo claim.",
                frames, checks, mixedFrames, maxNearDiameter, maxNearTrailWidth, maxRenderRadius, captures, lagSpread*100);
            File.WriteAllText("depth-motion-result.txt", result + "\n" + mathResult);
            Debug.Log(result);
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("depth-motion-result.txt", "FAIL: " + e);
            Debug.LogException(e);
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(1);
        }
    }

    static float DistanceAt(float t)
    {
        if (t < .25f) return .4f;
        if (t < .65f) return .4f * Mathf.Pow(1e6f / .4f, (t - .25f) / .4f);
        if (t < 1f) return 1e6f;
        if (t < 1.12f) return 1e6f * Mathf.Pow(.4f / 1e6f, (t - 1f) / .12f);
        if (t < 1.4f) return .4f;
        if (t < 1.65f) return 1e9f;
        return .4f;
    }

    void Capture(Camera camera, RenderTexture texture, string name)
    {
        camera.Render();
        RenderTexture.active = texture;
        var png = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        png.Apply();
        File.WriteAllBytes(Folder + "/" + name + ".png", png.EncodeToPNG());
        Destroy(png);
        captures++;
    }

    static bool Finite(Vector3 p) { return !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z); }
    void Require(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
}
#endif
