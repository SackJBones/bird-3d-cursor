#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Focused normal-quality Play Mode rendering; not synthetic gesture acceptance.
[DefaultExecutionOrder(32000)]
public class UnityVistaChecks : MonoBehaviour
{
    const string Active = "Bird.Vista.Checks", Folder = "../Validation/CoastalWorld/Vista01";
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");
            // ClientSim consumes/removes the SDK reference camera during startup.
            var reference = GameObject.Find("World camera settings");
            Require(reference != null && reference.GetComponent<Camera>().clearFlags == CameraClearFlags.Skybox, "Saved SDK reference camera uses skybox");
            var meshes = GameObject.Find("05 Coastal ridge and distant island/Coastal vista").GetComponentsInChildren<MeshFilter>();
            int triangles = meshes.Sum(f => f.sharedMesh.triangles.Length / 3);
            Require(meshes.Length == 3 && triangles < 2000, "Bounded scenery mesh cost");
            // Normal static batching uploads combined meshes without CPU-readable arrays.
            // Inspect saved source geometry before startup; render the actual batch afterward.
            foreach (var f in meshes)
            {
                var m = f.sharedMesh; Require(m.vertexCount == m.colors.Length, "Authored vertex colors: " + f.name);
                Require(m.vertices.All(v => !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude)), "Finite geometry: " + f.name);
                Require(m.normals.All(n => n.y > .05f), "Upward-facing nondegenerate terrain/sea: " + f.name);
            }
            SessionState.SetInt(Active + ".Triangles", triangles);
            SessionState.SetBool(Active, true); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartChecks() { if (SessionState.GetBool(Active, false)) new GameObject("Vista checks").AddComponent<UnityVistaChecks>(); }
    void LateUpdate()
    {
        if (!SessionState.GetBool(Active, false) || Time.timeSinceLevelLoad < 4) return;
        SessionState.SetBool(Active, false);
        try
        {
            string folder = Folder + "/" + EditorUserBuildSettings.activeBuildTarget; Directory.CreateDirectory(folder);
            Require(RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "Bird/Coastal daylight sky" && RenderSettings.skybox.shader.isSupported, "Saved supported daylight skybox");
            Require(RenderSettings.ambientMode == AmbientMode.Flat, "Existing flat ambient retained");
            var root = GameObject.Find("05 Coastal ridge and distant island/Coastal vista"); Require(root != null, "Nested editable vista");
            Require(root.GetComponentsInChildren<Collider>().Length == 0 && root.GetComponentsInChildren<Light>().Length == 0, "Scenery adds no collision or lighting");
            Require(!GameObject.Find("05 Coastal ridge and distant island/Quiet sea").GetComponent<Renderer>().enabled, "Old finite sea hidden; no coplanar overlap");
            var meshes = root.GetComponentsInChildren<MeshFilter>(); int triangles = SessionState.GetInt(Active + ".Triangles", 0);
            Require(meshes.Length == 3 && triangles > 0, "Three authored renderers survive normal batching");
            foreach (var f in meshes)
            {
                var r = f.GetComponent<Renderer>(); Require(r.sharedMaterial.shader.isSupported, "Supported shader: " + f.name);
                Require(r.shadowCastingMode == ShadowCastingMode.Off, "Distant scenery does not shadow the complex");
            }
            Require(LightmapSettings.lightmaps.Length > 0 && LightmapSettings.lightmaps.Length <= 4, "Interior baked maps retained");
            Require(FindObjectsOfType<Light>().All(l => l.lightmapBakeType == LightmapBakeType.Baked), "No realtime lights");
            Capture(folder, "01-arrival", new Vector3(0, 1.65f, -10), new Vector3(0, 2, 8), 70);
            Capture(folder, "02-water-overlook", new Vector3(15, -.35f, 47), new Vector3(130, 15, 220), 78);
            Capture(folder, "03-high-lookout", new Vector3(3, 22.65f, 36), new Vector3(130, 5, 220), 80);
            Capture(folder, "04-tide-room-seated", new Vector3(-22.082f, -.9f, 37.773f), new Vector3(-9.404f, -.9f, 64.962f), 85);
            Capture(folder, "05-open-coast", new Vector3(22, 2.65f, 32), new Vector3(270, 5, 900), 80);
            Capture(folder, "06-sea-side", new Vector3(20, -.35f, 51), new Vector3(1100, -12, 260), 75);
            Capture(folder, "07-exterior", new Vector3(65, 22, 78), new Vector3(-3, 10, 15), 70);
            Capture(folder, "08-inboard-floor-occlusion", new Vector3(3, 22.65f, 35), new Vector3(17, -1, 50), 78);
            File.WriteAllText(folder + "/inventory.txt", "Vista meshes=" + meshes.Length + "; triangles=" + triangles + "; added colliders/lights=0; maps=" + LightmapSettings.lightmaps.Length + "; quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + ". Eight normal-quality captures; no physical/multiplayer acceptance.");
            Finish(true, "Three editable scenery meshes, " + triangles + " triangles, supported sky/color shaders, no new collision or lights, baked maps retained, eight normal-quality rendered views.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Capture(string folder, string name, Vector3 eye, Vector3 target, float fov)
    {
        var camera = new GameObject("Vista evidence camera").AddComponent<Camera>(); camera.enabled = false; camera.transform.position = eye; camera.transform.LookAt(target);
        camera.fieldOfView = fov; camera.nearClipPlane = .03f; camera.farClipPlane = 10000; camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = ~((1 << 18) | (1 << 19));
        var texture = new RenderTexture(1200, 750, 24) { antiAliasing = 4 }; camera.targetTexture = texture; var image = new Texture2D(1200, 750, TextureFormat.RGB24, false);
        try { camera.Render(); RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); image.Apply(); File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG()); }
        finally { RenderTexture.active = null; camera.targetTexture = null; texture.Release(); DestroyImmediate(texture); DestroyImmediate(image); DestroyImmediate(camera.gameObject); }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Finish(bool pass, string message) { File.WriteAllText("coastal-vista-check-result.txt", (pass ? "PASS: " : "FAIL: ") + message); EditorApplication.Exit(pass ? 0 : 1); }
}
#endif
