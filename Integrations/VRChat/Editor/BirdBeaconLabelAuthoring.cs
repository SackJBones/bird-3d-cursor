#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicit presentation-only edit of the existing travel prefab.
public static class BirdBeaconLabelAuthoring
{
    public const string MaterialPath = BirdTeleportAuthoring.Folder + "/Beacon label.mat";
    public const string ShaderName = "Bird/Presentation/World Label";

    public static Material LabelMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        var shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("World label shader is unavailable.");
        material = new Material(shader) { name = "Beacon label" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    public static void Configure(Canvas canvas, bool back)
    {
        // uGUI glyph fronts face local -Z. Keep each label on its outward side.
        canvas.transform.localRotation = Quaternion.Euler(0, back ? 180 : 0, 0);
        var text = canvas.GetComponentInChildren<Text>(true);
        if (text == null) throw new InvalidOperationException("Beacon label requires ordinary uGUI Text.");
        text.material = LabelMaterial();
        text.color = new Color(.97f, .985f, 1f, 1f);
        text.raycastTarget = false;
        var outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.025f, .075f, .085f, 1f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);
        outline.useGraphicAlpha = true;
    }

    [MenuItem("Bird/Coastal world/Fix original beacon labels once")]
    public static void FixOriginalLabels()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            throw new InvalidOperationException("Label finish already authored; edit the saved material and prefab in the Inspector.");
        var root = PrefabUtility.LoadPrefabContents(BirdTeleportAuthoring.PrefabPath);
        try
        {
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            if (canvases.Length != 10 || canvases.Any(c => c.name != "Front label" && c.name != "Back label"))
                throw new InvalidOperationException("Expected exactly the original ten beacon label canvases.");
            foreach (var canvas in canvases) Configure(canvas, canvas.name == "Back label");
            PrefabUtility.SaveAsPrefabAsset(root, BirdTeleportAuthoring.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
#endif
