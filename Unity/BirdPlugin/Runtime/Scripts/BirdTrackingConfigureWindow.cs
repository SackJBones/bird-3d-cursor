using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public class BirdTrackingConfigureWindow : EditorWindow
{
    private string[] options = new string[] { "None", "Leap", "Oculus OVR" };
    private int selectedOption = 0;

    [MenuItem("Tools/Bird 3D Cursor/Configure Hand Tracking")]
    public static void ShowWindow()
    {
        GetWindow(typeof(BirdTrackingConfigureWindow), true, "Configure Hand Tracking");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Select Hand Tracking API", EditorStyles.boldLabel);
        selectedOption = EditorGUILayout.Popup("API", selectedOption, options);

        if (GUILayout.Button("Apply Settings"))
        {
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var allDefines = new HashSet<string>(definesString.Split(';'));

        // Clear previous settings
        allDefines.Remove("BIRD_LEAP_ENABLED");
        allDefines.Remove("BIRD_OCULUS_OVR_ENABLED");

        // Set new settings based on selection
        switch (options[selectedOption])
        {
            case "Leap":
                allDefines.Add("BIRD_LEAP_ENABLED");
                break;
            case "Oculus OVR":
                allDefines.Add("BIRD_OCULUS_OVR_ENABLED");
                break;
        }

        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", new List<string>(allDefines)));
    }
}
