using System;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mixpanel
{
    public class MixpanelSettings : ScriptableObject
    {
        private const string TrackUrlTemplate = "{0}track/?ip=1";
        private const string EngageUrlTemplate = "{0}engage/?ip=1";
        
        //TODO: Convert to log level
        [Tooltip("If true will print helpful debugging messages")] 
        public bool ShowDebug;
        [Tooltip("The api host of where to send the requests to. Useful when you need to proxy all the request to somewhere else.'")]
        public string APIHostAddress = "https://api.mixpanel.com/";
        [Tooltip("The token of the Mixpanel project.")]
        public string RuntimeToken = "";
        [Tooltip("Used when the DEBUG compile flag is set or when in the editor. Useful if you want to use different tokens for test builds.")]
        public string DebugToken = "";
        [Tooltip("Seconds (in realtime) between sending data to the API Host.")]
        public float FlushInterval = 60f;

        internal string Token {
            get {
                #if UNITY_EDITOR || DEBUG
                return DebugToken;
                #else
                return RuntimeToken;
                #endif
            }
        }

        #region static
        private static MixpanelSettings _instance;

        public static void LoadSettings()
        {
            if (!_instance)
            {
                _instance = FindOrCreateInstance();
                string host = _instance.APIHostAddress.EndsWith("/") ? _instance.APIHostAddress : $"{_instance.APIHostAddress}/";
                Config.TrackUrl = string.Format(TrackUrlTemplate, host);
                Config.EngageUrl = string.Format(EngageUrlTemplate, host);
                Config.ShowDebug = _instance.ShowDebug;
                Config.FlushInterval = _instance.FlushInterval;
            }
        }
    
        public static MixpanelSettings Instance {
            get {
                LoadSettings();
                
                // Be sure to respect the programmatic setting of values over the Unity UI's version of the settings (since they are set later)
                if (Config.ShowDebug != _instance.ShowDebug) {
                    Mixpanel.Log($"Mixpanel: Overriding ShowDebug from editor setting [{Config.ShowDebug}] with MixpanelSettings ShowDebug setting [{_instance.ShowDebug}]");
                    Config.ShowDebug = _instance.ShowDebug;
                }
                
                return _instance;
            }
        }

        private static MixpanelSettings FindOrCreateInstance()
        {
            MixpanelSettings instance = null;
            instance = instance ? null : Resources.Load<MixpanelSettings>("Mixpanel");
            instance = instance ? instance : Resources.LoadAll<MixpanelSettings>(string.Empty).FirstOrDefault();
            instance = instance ? instance : CreateAndSave<MixpanelSettings>();
            if (instance == null) throw new Exception("Could not find or create settings for Mixpanel");
            return instance;
        }

        private static T CreateAndSave<T>() where T : ScriptableObject
        {
            T instance = CreateInstance<T>();
#if UNITY_EDITOR
            //Saving during Awake() will crash Unity, delay saving until next editor frame
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () => SaveAsset(instance);
            }
            else
            {
                SaveAsset(instance);
            }
#endif
            return instance;
        }

#if UNITY_EDITOR
        private static void SaveAsset<T>(T obj) where T : ScriptableObject
        {

            string dirName = "Assets/Resources";
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            AssetDatabase.CreateAsset(obj, "Assets/Resources/Mixpanel.asset");
            AssetDatabase.SaveAssets();
        }
#endif
        #endregion
    }
}
