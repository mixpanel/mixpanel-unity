using UnityEditor;
using UnityEngine;

namespace mixpanel.editor
{
    [CustomEditor(typeof(MixpanelSettings))]
    internal class MixpanelSettingsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space();
            EditorGUI.DrawRect(EditorGUILayout.BeginVertical(), new Color(0.4f, 0.4f, 0.4f));
            EditorGUILayout.LabelField(new GUIContent("DistinctId", "The current distinct ID that will be sent in API calls."), new GUIContent(Mixpanel.DistinctId));
            EditorGUILayout.LabelField(new GUIContent("IsTracking", "The current value of the IsTracking property."), new GUIContent(Mixpanel.IsTracking.ToString()));
            EditorGUILayout.EndVertical();
        }
    }
}
