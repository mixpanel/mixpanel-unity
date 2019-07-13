using UnityEditor;

namespace mixpanel.editor
{
    internal class MixpanelSettingsEditor
    {
        [SettingsProvider]
        internal static SettingsProvider CreateCustomSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/Mixpanel")
            {
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    Editor.CreateEditor(MixpanelSettings.Instance).OnInspectorGUI();
                },
    
                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = SettingsProvider.GetSearchKeywordsFromSerializedObject(new SerializedObject(MixpanelSettings.Instance))
            };
    
            return provider;
        }
    }
}
