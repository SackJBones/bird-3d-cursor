#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Bird3DCursor.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Measures the shared display shell and logical world-depth occlusion.
// Parallel mono eye cameras are a geometry probe, not a headset stereo test.
public sealed class UnityDepthStereoChecks : MonoBehaviour
{
    const string Active = "Bird.DepthStereo.Checks";
    const string Folder = "DepthStereoCaptures";
    const int Pixels = 1024;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    RenderTexture texture;
    Texture2D readback;
    int captures, controls;
    struct Measurement { public int cyanPixels; public float centerX; public int maxX; }

    public static void Run()
    {
        File.WriteAllText("depth-stereo-result.txt", "PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        if (SessionState.GetBool(Active, false))
            new GameObject("Depth stereo probe").AddComponent<UnityDepthStereoChecks>();
    }

    void Start()
    {
        if (!SessionState.GetBool(Active, false)) return;
        try
        {
            Directory.CreateDirectory(Folder);
            texture = new RenderTexture(Pixels, Pixels, 24) { antiAliasing = 4 };
            readback = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            Camera center = CameraAt("Center", 0), left = CameraAt("Left", -.032f), right = CameraAt("Right", .032f);
            var visual = new GameObject("Shared display cursor").AddComponent<BirdDepthVisual>();
            visual.sizeMode = BirdDepthVisual.SizeMode.Inflation;
            visual.showTrail = false;
            var csv = new StringBuilder("ipd_m,logical_m,display_m,true_disparity_px,display_disparity_px,excess_disparity_px,left_centroid_px,right_centroid_px\n");
            float maxError = 0;
            foreach (float ipd in new[] { .058f, .064f, .072f })
            foreach (float distance in new[] { .4f, 2f, 4f, 20f, 100f, 1000f, 1e6f, 1e9f })
            {
                left.transform.position = Vector3.left * ipd * .5f;
                right.transform.position = Vector3.right * ipd * .5f;
                Vector3 logical = Vector3.forward * distance;
                visual.Clear();
                visual.Draw(logical, center, false, 0, 1f / 72);
                Vector3 displayed = visual.transform.Find("Sphere").position;
                float trueDisparity = ScreenX(left, logical) - ScreenX(right, logical);
                float displayDisparity = ScreenX(left, displayed) - ScreenX(right, displayed);
                float error = Mathf.Abs(displayDisparity - trueDisparity);
                maxError = Mathf.Max(maxError, error);
                Require(error < .14f, "Stereo shell error exceeded this fixture's bound");
                if (distance <= 100) Require(error < .0002f, "Unprojected working range gained stereo error");
                string leftPixel = "", rightPixel = "";
                if (ipd == .064f)
                {
                    string label = distance.ToString("G9", Invariant);
                    Measurement a = Capture(left, "stereo-" + label + "-left");
                    Measurement b = Capture(right, "stereo-" + label + "-right");
                    Require(a.cyanPixels > 0 && b.cyanPixels > 0, "Cursor absent in an eye image");
                    Require(Mathf.Abs(a.centerX-ScreenX(left,displayed)) < .6f && Mathf.Abs(b.centerX-ScreenX(right,displayed)) < .6f,
                        "Rendered center disagreed with projected center beyond raster tolerance");
                    leftPixel = a.centerX.ToString("F6", Invariant);
                    rightPixel = b.centerX.ToString("F6", Invariant);
                }
                csv.AppendFormat(Invariant, "{0:F3},{1:G9},{2:F6},{3:F6},{4:F6},{5:F6},{6},{7}\n",
                    ipd, distance, displayed.z, trueDisparity, displayDisparity, error, leftPixel, rightPixel);
            }
            File.WriteAllText(Folder + "/stereo.csv", csv.ToString());

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Opaque world occluder";
            var material = new Material(Shader.Find("Unlit/Color"));
            material.color = new Color(.55f, .045f, .025f);
            wall.GetComponent<Renderer>().sharedMaterial = material;
            var occlusion = new StringBuilder("logical_m,wall_m,expected_visible,observed_visible,cyan_pixels\n");
            float[] distances = { 2, 1000, 1000, 1000, 1e6f };
            float[] walls = { 1, 300, 600, 1500, 600 };
            int wrong = 0;
            for (int i = 0; i < distances.Length; i++)
            {
                float distance = distances[i], wallDistance = walls[i];
                wall.transform.position = Vector3.forward * wallDistance;
                wall.transform.localScale = new Vector3(wallDistance * .15f, wallDistance * .15f, .05f);
                visual.Clear();
                visual.Draw(Vector3.forward * distance, center, false, 0, 1f / 72);
                bool expectedVisible = distance < wallDistance;
                Measurement m = Capture(center, "occlusion-" + i);
                bool observedVisible = m.cyanPixels > 0;
                if (expectedVisible != observedVisible) wrong++;
                // Every world-depth case must agree, including beyond the shell.
                if (true)
                {
                    Require(observedVisible == expectedVisible, "Occlusion control failed");
                    controls++;
                }
                occlusion.AppendFormat(Invariant, "{0:G9},{1:G9},{2},{3},{4}\n",
                    distance, wallDistance, expectedVisible, observedVisible, m.cyanPixels);
            }
            // Direct-world reference: the 1000m point is inside the camera's
            // 2000m clip and must disappear behind the same 600m opaque wall.
            wall.transform.position = Vector3.forward * 600;
            wall.transform.localScale = new Vector3(90, 90, .05f);
            visual.gameObject.SetActive(false);
            var reference = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reference.name = "Independent direct-world reference";
            var referenceMaterial = new Material(Shader.Find("Unlit/Color"));
            referenceMaterial.color = Color.cyan;
            reference.GetComponent<Renderer>().sharedMaterial = referenceMaterial;
            reference.transform.position = Vector3.forward * 1000;
            reference.transform.localScale = Vector3.one * BirdDepthVisual.TargetDiameter(1000, BirdDepthVisual.SizeMode.Inflation);
            Require(Capture(center, "direct-world-behind-600m").cyanPixels == 0, "Direct-world occlusion reference failed");
            wall.SetActive(false);
            Require(Capture(center, "direct-world-no-wall").cyanPixels > 0, "Direct-world reference marker missing");
            controls += 2;
            reference.SetActive(false); visual.gameObject.SetActive(true);
            visual.Clear(); visual.Draw(Vector3.forward*1000,center,false,1,1f/72);
            int whole=Capture(center,"partial-control").cyanPixels;
            wall.SetActive(true); wall.transform.position=new Vector3(45,0,600);
            int half=Capture(center,"partial-occlusion").cyanPixels;
            Require(half>whole*.2f && half<whole*.8f,"Partial silhouette must occlude only covered fragments"); controls++;
            visual.Clear(); visual.showTrail=true; visual.style.showFarLocator=false;
            // One ribbon span crosses the 600m wall: independently, perspective
            // 1/z interpolation crosses at x=515px (400m -> 1000m endpoints).
            wall.transform.position=Vector3.forward*600;
            visual.Draw(new Vector3(-12,0,400),center,false,2,.02f);
            visual.Draw(new Vector3(30,0,1000),center,false,2.02f,.02f);
            var mixed=Capture(center,"mixed-depth-trail");
            Require(mixed.cyanPixels>5 && mixed.centerX<510 && mixed.maxX<=517,
                "Trail did not preserve separate logical depths: "+mixed.cyanPixels+" / "+mixed.centerX+" / "+mixed.maxX); controls++;
            // A complete far ribbon, outline and body must all disappear.
            visual.Clear(); visual.style.showFarLocator=true;
            for(int n=0;n<20;n++) visual.Draw(new Vector3((n-10)*3,0,1000),center,false,3+n*.02f,.02f);
            Require(Capture(center,"far-trail-hidden").cyanPixels==0,"Far trail leaked through world"); controls++;
            wall.SetActive(false); visual.Clear(); visual.showTrail=false;
            visual.style=new BirdDepthStyle { nearDiameter=2,showFarLocator=false };
            RenderSettings.skybox=new Material(Shader.Find("Skybox/Procedural"));
            center.clearFlags=CameraClearFlags.Skybox;
            visual.Draw(Vector3.forward*1e6f,center,false,4,.02f);
            Require(Capture(center,"beyond-clip-against-sky").cyanPixels>3,"Skybox painted over beyond-clip core"); controls++;
            Require(wrong==0,"World occlusion mismatch");
            File.WriteAllText(Folder + "/occlusion.csv", occlusion.ToString());
            string result = string.Format(Invariant,
                "PASS: 24 stereo configurations, max excess disparity={0:F6}px at 1024px/60deg, identity through 100m; {1} real Unity captures; {2} occlusion controls passed; {3}/5 logical-occlusion mismatches. Logical fragment depth agrees with opaque world controls, partial silhouettes and mixed-depth trails. GPU={4}; reversedZ={5}. Parallel mono-eye geometry only; no headset stereo/perception claim.",
                maxError, captures, controls, wrong, SystemInfo.graphicsDeviceType, SystemInfo.usesReversedZBuffer);
            File.WriteAllText("depth-stereo-result.txt", result);
            Debug.Log(result);
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("depth-stereo-result.txt", "FAIL: " + e);
            Debug.LogException(e);
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(1);
        }
    }

    Camera CameraAt(string name, float x)
    {
        var camera = new GameObject(name).AddComponent<Camera>();
        camera.enabled = false;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.transform.position = new Vector3(x, 0, 0);
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 2000;
        camera.fieldOfView = 60;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.012f, .018f, .035f);
        camera.targetTexture = texture;
        return camera;
    }

    static float ScreenX(Camera camera, Vector3 point) { return camera.WorldToViewportPoint(point).x * Pixels; }

    Measurement Capture(Camera camera, string name)
    {
        camera.Render();
        RenderTexture.active = texture;
        readback.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
        readback.Apply();
        Color32[] data = readback.GetPixels32();
        double weightedX = 0, total = 0;
        int count = 0, maxX = -1;
        for (int i = 0; i < data.Length; i++)
        {
            Color32 c = data[i];
            int strength = Math.Min(c.g, c.b)-c.r;
            if (strength < 20) continue;
            weightedX += (i % Pixels + .5) * strength;
            total += strength;
            count++; maxX=Math.Max(maxX,i%Pixels);
        }
        File.WriteAllBytes(Folder + "/" + name + ".png", readback.EncodeToPNG());
        captures++;
        return new Measurement { cyanPixels = count, maxX=maxX, centerX = total > 0 ? (float)(weightedX / total) : -1 };
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
#endif
