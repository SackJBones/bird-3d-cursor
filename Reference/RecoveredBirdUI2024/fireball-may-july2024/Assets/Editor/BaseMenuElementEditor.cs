using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BaseMenuElement))]
public class BaseMenuElementEditor : Editor
{
    private MenuElementState previewState;

    // Create an array to hold foldout states
    private bool[] foldoutStates;

    public override void OnInspectorGUI()
    {
        BaseMenuElement element = (BaseMenuElement)target;

        // Add a dropdown to select the preview state
        previewState = (MenuElementState)EditorGUILayout.EnumPopup("Preview State", previewState);
        
        if (GUILayout.Button("Preview State"))
        {
            ApplyPreviewState(element, previewState);
        }

        // You can still draw the default inspector for other non-custom fields
        DrawDefaultInspector();

        if (element == null)
            return;

        // Check if the array has been initialized, if not, do so
        if (foldoutStates == null || foldoutStates.Length != element.VisualFeatures.Count)
        {
            int? count = element.VisualFeatures.Count;  
            foldoutStates = new bool[count ?? 0];
        }

        for (int i = 0; i < element.VisualFeatures.Count; i++)
        {
            VisualFeature feature = element.VisualFeatures[i];

            // delimit the start of the feature
            EditorGUILayout.Space();
            // foldout
            // Use the name of the feature for the editor display, unless it's unassigned, in which case use its integer index in the array as a label
            string featureNameLabel = "Visual Feature (" + (feature.visualObject == null ? i.ToString() : feature.visualObject.name) + ")";

            // Use the saved foldout state
            foldoutStates[i] = EditorGUILayout.Foldout(foldoutStates[i], featureNameLabel);
            if (!foldoutStates[i])
            {
                // if the foldout is closed, skip to the next feature
                continue;
            }

            EditorGUILayout.LabelField("Visual Feature Properties", EditorStyles.boldLabel);

            // show the fields for each of the VisualFeatureProperties.
            feature.visualObject = (GameObject)EditorGUILayout.ObjectField("Visual Element", feature.visualObject, typeof(GameObject), true);

            // Then do this for each state in the VisualFeature
            feature.InactiveState = DrawPropertiesForState(feature.visualObject, feature.InactiveState, "Inactive State");
            feature.EnabledState = DrawPropertiesForState(feature.visualObject, feature.EnabledState, "Enabled State");
            feature.HighlightedState = DrawPropertiesForState(feature.visualObject, feature.HighlightedState, "Highlighted State");
            feature.ActivatedState = DrawPropertiesForState(feature.visualObject, feature.ActivatedState, "Activated State");
            feature.BackgroundState = DrawPropertiesForState(feature.visualObject, feature.BackgroundState, "Background State");
        }
    }

private void DrawVector3FieldWithApplyAndRecordButtons(GameObject element, ref Vector3 property, string label, System.Action<GameObject, Vector3> applyAction, Func<GameObject, Vector3> recordAction)
    {
        EditorGUILayout.BeginHorizontal();
        property = EditorGUILayout.Vector3Field(label, property);
        if (GUILayout.Button("Apply") && element != null)
        {
            // record object for undo
            Undo.RecordObject(element, "Apply " + label);
            applyAction?.Invoke(element, property);
        }
        if (GUILayout.Button("Record") && element != null)
        {
            property = recordAction.Invoke(element);
        }
        EditorGUILayout.EndHorizontal();
    }

private void DrawRotationFieldWithApplyAndRecordButtons(GameObject element, ref Quaternion property, string label, System.Action<GameObject, Quaternion> applyAction, Func<GameObject, Quaternion> recordAction)
{
    EditorGUILayout.BeginHorizontal();
    // Only convert to Euler angles for display in the editor field
    Vector3 euler = property.eulerAngles;
    Quaternion originalProperty = property;
    EditorGUI.BeginChangeCheck();
    // Get the Euler angles from the editor field
    euler = EditorGUILayout.Vector3Field(label, euler);
    if (EditorGUI.EndChangeCheck())
    {
        // If the user has changed the value, convert back to a Quaternion
        property = Quaternion.Euler(euler);
    }
    else
    {
        property = originalProperty;
    }
    if (GUILayout.Button("Apply") && element != null)
    {
        // record object for undo
        Undo.RecordObject(element, "Apply " + label);
        // Use the Quaternion for the apply action
        applyAction?.Invoke(element, property);
    }
    if (GUILayout.Button("Record") && element != null)
    {
        property = recordAction.Invoke(element);
    }
    EditorGUILayout.EndHorizontal();
}

private void DrawColorFieldWithApplyAndRecordButtons(GameObject element, ref Color property, string label, System.Action<GameObject, Color> applyAction, Func<GameObject, Color> recordAction)
    {
        EditorGUILayout.BeginHorizontal();
        property = EditorGUILayout.ColorField(label, property);
        if (GUILayout.Button("Apply") && element != null)
        {
            // record object for undo
            Undo.RecordObject(element, "Apply " + label);
            applyAction?.Invoke(element, property);
        }
        if (GUILayout.Button("Record") && element != null)
        {
            property = recordAction.Invoke(element);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void RecordAllProperties(GameObject element, VisualFeatureProperties properties)
    {
        if (element != null)
        {
            properties.localPosition = element.transform.localPosition;
            properties.localRotation = element.transform.localRotation;
            properties.scale = element.transform.localScale;
            Renderer renderer = element.GetComponent<Renderer>();
            if (renderer != null)
            {
                properties.color = renderer.sharedMaterial.color;
            }
        }
    }

    private void ApplyPreviewState(BaseMenuElement element, MenuElementState state)
    {
        foreach (VisualFeature feature in element.VisualFeatures)
        {
            VisualFeatureProperties properties = feature.GetPropertiesForState(state);

            // Apply the properties to the visual object
            feature.visualObject.transform.localPosition = properties.localPosition;
            feature.visualObject.transform.localRotation = properties.localRotation;
            feature.visualObject.transform.localScale = properties.scale;

            Renderer renderer = feature.visualObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = properties.color;
            }
        }
    }

    private VisualFeatureProperties DrawPropertiesForState(GameObject element, VisualFeatureProperties properties, string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        DrawVector3FieldWithApplyAndRecordButtons(
            element,
            ref properties.localPosition,
            "Local Position",
            (GameObject obj, Vector3 pos) => obj.transform.localPosition = pos,
            (GameObject obj) => obj.transform.localPosition
        );

        DrawVector3FieldWithApplyAndRecordButtons(
            element,
            ref properties.scale,
            "Scale",
            (GameObject obj, Vector3 scale) => obj.transform.localScale = scale,
            (GameObject obj) => obj.transform.localScale
        );

        DrawRotationFieldWithApplyAndRecordButtons(
            element,
            ref properties.localRotation,
            "Rotation",
            (GameObject obj, Quaternion rotation) => obj.transform.localRotation = rotation,
            (GameObject obj) => obj.transform.localRotation
        );

        DrawColorFieldWithApplyAndRecordButtons(
            element,
            ref properties.color,
            "Color",
            (GameObject obj, Color color) =>
            {
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial.color = color;
                }
            },
            (GameObject obj) =>
            {
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.sharedMaterial.color;
                }
                return Color.white;
            }
        );

        // Add a button to record the current properties of the element
        if (GUILayout.Button("Record All Properties"))
        {
            RecordAllProperties(element, properties);
        }

        return properties;
    }

    // private VisualFeatureProperties DrawPropertiesForState(GameObject element, VisualFeatureProperties properties, string title)
    // {
    //     EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

    //     Indent();
    //     EditorGUILayout.BeginHorizontal();
    //     properties.localPosition = EditorGUILayout.Vector3Field("Local Position", properties.localPosition);
    //     if (GUILayout.Button("Apply"))
    //     {
    //         // record object for undo
    //         Undo.RecordObject(element.transform, "Apply Local Position");
    //         element.transform.localPosition = properties.localPosition;
    //     }
    //     if (GUILayout.Button("Record"))
    //     {
    //         properties.localPosition = element.transform.localPosition;
    //     }
    //     EditorGUILayout.EndHorizontal();

    //     EditorGUILayout.BeginHorizontal();
    //     properties.scale = EditorGUILayout.Vector3Field("Scale", properties.scale);
    //     if (GUILayout.Button("Apply"))
    //     {
    //         // record object for undo
    //         Undo.RecordObject(element.transform, "Apply Scale");
    //         element.transform.localScale = properties.scale;
    //     }
    //     if (GUILayout.Button("Record"))
    //     {
    //         properties.scale = element.transform.localScale;
    //     }
    //     EditorGUILayout.EndHorizontal();

    //     EditorGUILayout.BeginHorizontal();
    //     properties.color = EditorGUILayout.ColorField("Color", properties.color);
    //     if (GUILayout.Button("Apply"))
    //     {
    //         Renderer renderer = element.GetComponent<Renderer>();
    //         // record object for undo
    //         Undo.RecordObject(renderer.sharedMaterial, "Apply Color");
    //         if (renderer != null)
    //         {
    //             renderer.sharedMaterial.color = properties.color;
    //         }
    //     }
    //     if (GUILayout.Button("Record"))
    //     {
    //         Renderer renderer = element.GetComponent<Renderer>();
    //         if (renderer != null)
    //         {
    //             properties.color = renderer.sharedMaterial.color;
    //         }
    //     }
    //     EditorGUILayout.EndHorizontal();

    //     // Add a button to record the current properties of the element
    //     EditorGUILayout.BeginHorizontal();
    //     EditorGUILayout.LabelField("All Current Properties");
    //     if (GUILayout.Button("Apply", EditorStyles.boldLabel))
    //     {
    //         // Check if GameObject is not null
    //         if (element != null)
    //         {
    //             // record objects for undo
    //             Undo.RecordObject(element.transform, "Apply All Properties");

    //             // Apply transform properties
    //             element.transform.localPosition = properties.localPosition;
    //             element.transform.localScale = properties.scale;

    //             // Get Renderer component if available
    //             Renderer renderer = element.GetComponent<Renderer>();
    //             if (renderer != null)
    //             {
    //                 // record object for undo
    //                 Undo.RecordObject(renderer.sharedMaterial, "Apply All Properties");
    //                 renderer.sharedMaterial.color = properties.color;
    //             }
    //         }
    //     }
    //     if (GUILayout.Button("Record", EditorStyles.boldLabel))
    //     {
    //         // Check if GameObject is not null
    //         if (element != null)
    //         {
    //             // Record transform properties
    //             properties.localPosition = element.transform.localPosition;
    //             properties.scale = element.transform.localScale;

    //             // Get color from Renderer's material if available
    //             Renderer renderer = element.GetComponent<Renderer>();
    //             if (renderer != null)
    //             {
    //                 properties.color = renderer.sharedMaterial.color;
    //             }
    //         }
    //     }
    //     EditorGUILayout.EndHorizontal();
    //     Dedent();

    //     return properties;
    // }
}
