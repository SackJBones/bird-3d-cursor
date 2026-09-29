#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;

public static class BirdTeleportAuthoring
{
    public const string Folder = "Assets/BirdWorld/TeleportBeacons";
    public const string PrefabPath = Folder + "/Bird travel network.prefab";

    [MenuItem("Bird/Coastal world/Add beacon targeting preview once")]
    public static async void AddFromMenu() { await Add(); }
    public static async Task Add()
    {
        if (File.Exists(PrefabPath)) throw new InvalidOperationException("Beacons already authored; edit the saved prefab and scene bindings.");
        var scene = EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        if (UnityEngine.Object.FindObjectOfType<BirdTeleportRouter>(true) != null) throw new InvalidOperationException("Travel network already exists.");
        var station = UnityEngine.Object.FindObjectOfType<BirdPersonalStation>(true);
        if (station == null) throw new InvalidOperationException("Personal Bird must be present first.");
        var hands = station.personalRig.GetComponentsInChildren<BirdAvatarHandInput>(true).OrderBy(h => h.rightHand).ToArray();
        if (hands.Length != 2) throw new InvalidOperationException("Expected both local hand pipelines.");
        foreach (string name in new[] { "BirdTeleportBeacon", "BirdTeleportRouter", "BirdUiPointer", "BirdAvatarUiInput" })
        {
            var source = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/" + name + ".cs");
            if (source == null) throw new InvalidOperationException("Missing source " + name);
            string path = "Assets/BirdWorld/Programs/" + name + ".asset";
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            if (program == null)
            { program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript = source; AssetDatabase.CreateAsset(program, path); }
            else if (program.sourceCsScript != source) throw new InvalidOperationException("Program conflict " + name);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        var programs = new[] { "BirdTeleportBeacon", "BirdTeleportRouter", "BirdUiPointer", "BirdAvatarUiInput" }
            .Select(n=>AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/BirdWorld/Programs/"+n+".asset")).ToArray();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (programs.Any(p=>p.ScriptVersion<UdonSharpProgramVersion.CurrentVersion) && DateTime.UtcNow<deadline) await Task.Delay(100);
        if (programs.Any(p=>p.ScriptVersion<UdonSharpProgramVersion.CurrentVersion)) throw new InvalidOperationException("Udon program migration did not finish.");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new InvalidOperationException("Udon compile failed.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Beacon ring.mat");
        if (material == null) { material = new Material(Shader.Find("Unlit/Color")) { color = new Color(.12f,.55f,.62f) }; AssetDatabase.CreateAsset(material, Folder + "/Beacon ring.mat"); }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Vertical torus.asset");
        if (mesh == null) { mesh = Torus(); AssetDatabase.CreateAsset(mesh, Folder + "/Vertical torus.asset"); }
        var root = new GameObject("Bird travel network");
        try
        {
            var router = root.AddUdonSharpComponent<BirdTeleportRouter>();
            router.allowTeleport = false;
            router.pointers = new BirdUiPointer[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "Left targeting input" : "Right targeting input"); go.transform.SetParent(root.transform, false);
                var pointer = go.AddUdonSharpComponent<BirdUiPointer>();
                var adapter = go.AddUdonSharpComponent<BirdAvatarUiInput>(); adapter.pointer = pointer;
                router.pointers[i] = pointer;
            }
            string[] names = { "Arrival", "Conversation terrace", "Upper terrace", "High lookout", "Water garden" };
            Vector3[] positions = { new Vector3(-3,1.7f,-1.5f),new Vector3(15.5f,2.7f,31),new Vector3(18.8f,16.2f,20),new Vector3(14.8f,24.2f,34),new Vector3(23.5f,1.2f,54) };
            Vector3[] feet = { new Vector3(-4.5f,0,-2),new Vector3(14,1,29),new Vector3(17,13,20),new Vector3(13,21,35),new Vector3(23.5f,-2,53) };
            float[] yaw = { 0,25,10,30,20 };
            router.beacons = new BirdTeleportBeacon[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var go = new GameObject("Beacon / " + names[i]); go.transform.SetParent(root.transform, false);
                go.transform.position = positions[i]; go.transform.rotation = Quaternion.Euler(0,yaw[i],0);
                var beacon = go.AddUdonSharpComponent<BirdTeleportBeacon>(); router.beacons[i] = beacon;
                var visual = new GameObject("Vertical ring", typeof(MeshFilter), typeof(MeshRenderer)); visual.transform.SetParent(go.transform, false);
                visual.GetComponent<MeshFilter>().sharedMesh = mesh; beacon.ring = visual.GetComponent<MeshRenderer>(); beacon.ring.sharedMaterial = material;
                beacon.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; beacon.ring.receiveShadows = false;
                var landing = new GameObject("Clear landing beside beacon").transform; landing.SetParent(go.transform, false); landing.position = feet[i];
                beacon.landing = landing;
                Label(go.transform, names[i], false); Label(go.transform, names[i], true);
            }
            // Save a reusable network with internal references only. Existing local
            // Bird bindings belong to this scene instance, not to the prefab asset.
            foreach (var proxy in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, GameObject.Find("06 Experience anchors").transform);
            var savedRouter = instance.GetComponent<BirdTeleportRouter>(); savedRouter.station = station;
            var adapters = instance.GetComponentsInChildren<BirdAvatarUiInput>();
            for (int i = 0; i < 2; i++) adapters[i].input = hands[i];
            foreach (var proxy in instance.GetComponentsInChildren<UdonSharpBehaviour>(true))
            {
                UdonSharpEditorUtility.CopyProxyToUdon(proxy);
                PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
                PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy));
            }
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            AssetDatabase.SaveAssets();
        }
        finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    }

    static void Label(Transform parent, string title, bool back)
    {
        var canvas = new GameObject(back ? "Back label" : "Front label", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.transform.SetParent(parent, false);
        canvas.transform.localPosition = new Vector3(0,-1.12f,back ? .025f : -.025f);
        canvas.transform.localScale = Vector3.one * .001f;
        var label = new GameObject("Destination", typeof(RectTransform)).AddComponent<Text>(); label.transform.SetParent(canvas.transform, false);
        label.rectTransform.sizeDelta = new Vector2(2400,380); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 100; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(.06f,.33f,.38f); label.raycastTarget = false;
        label.text = title + "\nPoint through to highlight"; label.fontSize = 76;
        BirdBeaconLabelAuthoring.Configure(canvas, back);
    }
    static Mesh Torus()
    {
        const int major = 48, minor = 8;
        var vertices = new Vector3[major * minor]; var normals = new Vector3[vertices.Length]; var triangles = new int[major * minor * 6];
        for (int i = 0; i < major; i++) for (int j = 0; j < minor; j++)
        {
            float a = i * Mathf.PI * 2 / major, b = j * Mathf.PI * 2 / minor;
            Vector3 radial = new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            int index = i * minor + j; normals[index] = radial * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
            vertices[index] = radial * .82f + normals[index] * .075f;
            int next = ((i+1)%major)*minor+j, wrap = i*minor+(j+1)%minor, diagonal = ((i+1)%major)*minor+(j+1)%minor, k=index*6;
            triangles[k]=index;triangles[k+1]=next;triangles[k+2]=diagonal;triangles[k+3]=index;triangles[k+4]=diagonal;triangles[k+5]=wrap;
        }
        var mesh = new Mesh { name = "Vertical torus", vertices = vertices, normals = normals, triangles = triangles }; mesh.RecalculateBounds(); return mesh;
    }
}
#endif
