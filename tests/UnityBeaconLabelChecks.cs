#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public partial class UnityCoastalWorldChecks
{
    public static void FixBeaconLabels()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            BeaconLabelViews(Folder + "/Label01/Before");
            string before = BeaconGeometry();
            BirdBeaconLabelAuthoring.FixOriginalLabels();
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            Require(before == BeaconGeometry(), "Beacon positions, target radii, landings, ring meshes and permissions unchanged");
            Finish("coastal-labels-fix", true, "Ten original text faces use outward-facing, depth-tested world UI material and subtle outline. Beacon geometry, landing and saved travel permission unchanged.");
        }
        catch (Exception e) { Finish("coastal-labels-fix", false, e.ToString()); }
    }

    static string BeaconGeometry()
    {
        var router = UnityEngine.Object.FindObjectOfType<BirdTeleportRouter>();
        return router.allowTeleport + "\n" + string.Join("\n", router.beacons.Select(b =>
            b.name + b.transform.localToWorldMatrix.ToString("R") + b.targetRadius.ToString("R") +
            b.landing.localToWorldMatrix.ToString("R") + AssetDatabase.GetAssetPath(b.ring.GetComponent<MeshFilter>().sharedMesh)));
    }

    public static void CheckBeaconLabels()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            var router = UnityEngine.Object.FindObjectOfType<BirdTeleportRouter>();
            Require(router != null && !router.allowTeleport, "Saved travel permission stays off");
            var texts = router.GetComponentsInChildren<Text>(true);
            Require(texts.Length == 10, "Two original text faces per beacon");
            foreach (var text in texts)
            {
                Require(text.material.shader.name == BirdBeaconLabelAuthoring.ShaderName && !ShaderUtil.ShaderHasError(text.material.shader), "Supported compiled label shader");
                Require(!text.raycastTarget && text.GetComponent<Outline>() != null, "Noninteractive outlined text");
                var canvas = text.GetComponentInParent<Canvas>();
                bool back = canvas.name == "Back label";
                Require(Vector3.Dot(canvas.transform.localRotation * Vector3.back, back ? Vector3.forward : Vector3.back) > .999f, "Outward glyph face");
                Require(text.text.EndsWith("Point through to highlight"), "Truthful targeting-preview instruction");
            }
            string folder = Folder + "/Label01/" + EditorUserBuildSettings.activeBuildTarget;
            Directory.CreateDirectory(folder);
            int samples = CheckLabelFaces(router.beacons.Last().transform, folder);
            BeaconLabelViews(folder);
            string result = "Ten outward text faces; " + samples + " rendered front/rear/oblique face-isolation comparisons, plus normal depth occlusion; seven context views. No targeting, geometry, landing or permission changes. Not physical text comfort acceptance.";
            File.WriteAllText(folder + "/inventory.txt", result);
            Finish("coastal-labels-check", true, result);
        }
        catch (Exception e) { Finish("coastal-labels-check", false, e.ToString()); }
    }

    static int CheckLabelFaces(Transform beacon, string folder)
    {
        var root = new GameObject("Isolated label render fixture");
        var cameraObject = new GameObject("Label fixture camera");
        var camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(1024, 512, 24);
        var texture = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
        GameObject blocker = null;
        try
        {
            root.transform.position = new Vector3(500, 500, 500);
            var front = UnityEngine.Object.Instantiate(beacon.Find("Front label").gameObject, root.transform, false);
            var back = UnityEngine.Object.Instantiate(beacon.Find("Back label").gameObject, root.transform, false);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = .65f;
            camera.nearClipPlane = .03f; camera.farClipPlane = 10; camera.targetTexture = target;
            Vector3 center = root.transform.position + Vector3.down * 1.12f;
            Func<Color32[]> pixels = () =>
            {
                Canvas.ForceUpdateCanvases(); camera.Render();
                var old = RenderTexture.active;
                try { RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,1024,512),0,0); texture.Apply(); return texture.GetPixels32(); }
                finally { RenderTexture.active = old; }
            };
            int count = 0;
            foreach (float angle in new float[] {0,45,85,95,135,180,225,275,315})
            {
                float a = angle * Mathf.Deg2Rad;
                camera.transform.position = center + new Vector3(Mathf.Sin(a),0,-Mathf.Cos(a)) * 3.5f;
                camera.transform.LookAt(center);
                front.SetActive(true); back.SetActive(true); var both = pixels();
                File.WriteAllBytes(folder + "/face-" + angle + ".png", texture.EncodeToPNG());
                var visible = Mathf.Cos(a) > 0 ? front : back;
                var hidden = visible == front ? back : front;
                hidden.SetActive(false); var one = pixels();
                Require(both.Where((p,i) => Math.Abs(p.r-one[i].r)+Math.Abs(p.g-one[i].g)+Math.Abs(p.b-one[i].b)>3).Count() < 4, "Opposed face does not bleed through at " + angle);
                Require(one.Count(p => p.r > 80 && p.g > 80 && p.b > 80) > 30, "Outward label is actually visible at " + angle);
                visible.SetActive(false); hidden.SetActive(true); var rear = pixels();
                Require(rear.All(p => p.r < 3 && p.g < 3 && p.b < 3), "Rear face is culled at " + angle);
                count++;
            }
            front.SetActive(true); back.SetActive(true);
            camera.transform.position = center - Vector3.forward * 3.5f; camera.transform.LookAt(center);
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.layer = 30;
            blocker.transform.position = center - Vector3.forward; blocker.transform.localScale = new Vector3(3,2,.1f);
            blocker.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BirdCoastalWorldAuthoring.Folder + "/Materials/Plaster.mat");
            var withText = pixels(); front.SetActive(false); back.SetActive(false); var noText = pixels();
            Require(withText.SequenceEqual(noText), "Ordinary opaque geometry hides labels");
            return count;
        }
        finally
        {
            if (blocker != null) UnityEngine.Object.DestroyImmediate(blocker);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject);
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void BeaconLabelViews(string folder)
    {
        Directory.CreateDirectory(folder); Canvas.ForceUpdateCanvases();
        PondCapture(folder,"01-east-approach",new Vector3(23.5f,-.35f,49),new Vector3(10,-1.9f,62),85);
        var water = UnityEngine.Object.FindObjectsOfType<BirdTeleportBeacon>().First(b => b.name == "Beacon / Water garden").transform;
        Vector3 center = water.TransformPoint(new Vector3(0,-.8f,0));
        PondCapture(folder,"02-water-front-free",center-water.forward*3.5f,center,65);
        PondCapture(folder,"03-water-back-free",center+water.forward*3.5f,center,65);
        PondCapture(folder,"04-water-oblique-free",center+(water.right*.866f-water.forward*.5f)*3.5f,center,65);
        PondCapture(folder,"05-water-edge-free",center+(water.right*.996f-water.forward*.087f)*3.5f,center,65);
        PondCapture(folder,"06-high-return",new Vector3(13.7f,22.65f,35.7f),new Vector3(23.5f,1.2f,54),85);
        PondCapture(folder,"07-arrival",new Vector3(0,1.65f,-10),new Vector3(0,2,8),75);
    }
}
#endif
