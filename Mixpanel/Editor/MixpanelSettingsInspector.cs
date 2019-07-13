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
            EditorGUILayout.LabelField("DistinctId", Mixpanel.DistinctId);
            EditorGUILayout.LabelField("IsTracking", Mixpanel.IsTracking.ToString());
            EditorGUILayout.EndVertical();
        }
    }
}