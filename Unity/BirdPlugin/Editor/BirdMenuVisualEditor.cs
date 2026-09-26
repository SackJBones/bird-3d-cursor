using Bird3DCursor.UI;
using UnityEditor;

namespace Bird3DCursor.Editor
{
    [CustomEditor(typeof(BirdMenuVisual))]
    [CanEditMultipleObjects]
    public sealed class BirdMenuVisualEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(targets.Length!=1) return;
            string error=((BirdMenuVisual)target).ConfigurationError;
            if(error!=null) EditorGUILayout.HelpBox(error,MessageType.Warning);
            else EditorGUILayout.HelpBox("State poses are relative to the bound artwork's rest transform. Keep hit colliders on the stationary control. Disabling this component restores the original artwork and property block.",MessageType.Info);
        }
    }
}
