using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace mixpanel.serialization.Utilities.Editor
{
    [InitializeOnLoad]
    public class MixpanelInstaller
    {
        private static string SerializationCheckPath => "Assets/Plugins/Mixpanel/Odin Serializer/OdinBuildAutomation.cs";

        static MixpanelInstaller()
        {
            if (!File.Exists(SerializationCheckPath))
            {
                Debug.Log("Install Mixpanel Serialization");
            }
            
            
        }
    }
}
