using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mixpanel
{
    public class MixpanelSettings : ScriptableObject
    {
        private const string TrackUrlTemplate = "{0}track/?ip={1}";
        private const string EngageUrlTemplate = "{0}engage/?ip={1}";
        private const string GroupsUrlTemplate = "{0}groups/?ip={1}";
        
        //TODO: Convert to log level
        [Tooltip("If true will print helpful debugging messages")] 
        public bool ShowDebug;
        [Tooltip("If true, you need to manually initialize the library")]
        public bool ManualInitialization;
        [Tooltip("The api host of where to send the requests to. Useful when you need to proxy all the request to somewhere else.'")]
        public string APIHostAddress = "https://api.mixpanel.com/";
        [Tooltip("The token of the Mixpanel project.")]
        public string RuntimeToken = "";
        [Tooltip("Used when the DEBUG compile flag is set or when in the editor. Useful if you want to use different tokens for test builds.")]
        public string DebugToken = "";
        [Tooltip("Seconds (in realtime) between sending data to the API Host.")]
        public float FlushInterval = 60f;
        [Tooltip("If true, the library will use the IP address of the client for geolocation. If false, the library will use the IP address of the Mixpanel server for geolocation.")]
        public bool UseIpAddressForGeolocation = true;
        /// <summary>
        /// Tracks for a project that uses hierarchical groups. Set this to match the project from the first
        /// launch; it is not meant to be changed for an existing install, and it can't change while
        /// %Mixpanel is initialized.
        /// </summary>
        /// <description>
        /// <p>On a hierarchical groups project %Mixpanel builds the user's distinct id from <c>$company_id</c>
        /// and <c>$user_id</c>, so the company is part of the user's identity. When enabled:</p>
        /// <ul>
        ///   <li>Identify with <c>Mixpanel.Identify(new MixpanelIdentity(userId, companyId))</c>. Every event then
        ///       carries <c>$company_id</c> beside <c>$user_id</c> and <c>$device_id</c>.</li>
        ///   <li><c>Mixpanel.People</c> updates are sent to <c>/groups</c> as the <c>$user_id</c> group profile
        ///       within the company. <c>/groups</c> has no <c>$add</c>, <c>$append</c> or <c>$merge</c>, so
        ///       Increment, Append and TrackCharge are dropped with an error log.</li>
        ///   <li><c>Mixpanel.Group</c> updates carry the current <c>$company_id</c>, and are dropped until a
        ///       company is known.</li>
        /// </ul>
        /// <p>Defaults to false, which behaves exactly as before.</p>
        /// </description>
        [Tooltip("If true, tracks for a project that uses hierarchical groups: identify users with Mixpanel.Identify(new MixpanelIdentity(userId, companyId)), and user profile updates are sent to /groups. Set it from the first launch; it can't change while Mixpanel is initialized.")]
        public bool HierarchicalGroups = false;

        internal string Token {
            get {
                #if UNITY_EDITOR || DEBUG
                return DebugToken;
                #else
                return RuntimeToken;
                #endif
            }
        }

        public void ApplyToConfig()
        {
            string host = APIHostAddress.EndsWith("/") ? APIHostAddress : $"{APIHostAddress}/";
            string useIpAddressForGeolocation = UseIpAddressForGeolocation ? "1" : "0";
            Config.TrackUrl = string.Format(TrackUrlTemplate, host, useIpAddressForGeolocation);
            Config.EngageUrl = string.Format(EngageUrlTemplate, host, useIpAddressForGeolocation);
            Config.GroupsUrl = string.Format(GroupsUrlTemplate, host, useIpAddressForGeolocation);
            Config.ShowDebug = ShowDebug;
            Config.ManualInitialization = ManualInitialization;
            Config.FlushInterval = FlushInterval;
            // Hierarchical groups is fixed for the life of an initialized instance, since it changes how
            // identity is stored and where profile updates are sent.
            if (!Controller.IsInitialized())
            {
                Config.HierarchicalGroups = HierarchicalGroups;
            }
        }

        #region static
        private static MixpanelSettings _instance;

        public static void LoadSettings()
        {
            if (!_instance)
            {
                _instance = FindOrCreateInstance();
                _instance.ApplyToConfig();
            }
        }

        public static MixpanelSettings Instance {
            get {
                LoadSettings();
                return _instance;
            }
        }

        private static MixpanelSettings FindOrCreateInstance()
        {
            MixpanelSettings instance = Resources.Load<MixpanelSettings>("Mixpanel");
            if (instance == null)
            {
                // Preserve compatibility with existing projects that stored the asset
                // elsewhere under Resources with a different name.
                MixpanelSettings[] existingInstances = Resources.LoadAll<MixpanelSettings>(string.Empty);
                if (existingInstances != null && existingInstances.Length > 0)
                    instance = existingInstances[0];
            }
            if (instance == null)
                instance = CreateAndSave<MixpanelSettings>();
            if (instance == null)
                throw new Exception("Could not find or create settings for Mixpanel");
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
