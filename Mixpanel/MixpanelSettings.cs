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
        [Tooltip("If true will print helpful debugging messages")]
        public bool ShowDebug = false;
        [Tooltip("The token of the Mixpanel project.")]
        public string RuntimeToken = "";
        [Tooltip("Used when the DEBUG compile flag is set or when in the editor. Useful if you want to use different tokens for test builds.")]
        public string DebugToken = "";

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
    
	    public static MixpanelSettings Instance {
		    get {
			    if (!_instance) _instance = FindOrCreateInstance();
			    return _instance;
		    }
	    }
	    
	    private static MixpanelSettings FindOrCreateInstance()
		{
	    	MixpanelSettings instance = null;
		    instance = instance ? instance : Resources.Load<MixpanelSettings>("Mixpanel");
		    instance = instance ? instance : Resources.LoadAll<MixpanelSettings>(string.Empty).FirstOrDefault() as MixpanelSettings;
		    instance = instance ? instance : CreateAndSave<MixpanelSettings>();
	    	if (instance == null) throw new Exception("Could not find or create settings for Mixpanel");
		    return instance;
	    }

		private static T CreateAndSave<T>() where T : ScriptableObject
		{
			T instance = ScriptableObject.CreateInstance<T>();
#if UNITY_EDITOR
			//Saving during Awake() will crash Unity, delay saving until next editor frame
			if(EditorApplication.isPlayingOrWillChangePlaymode){
				EditorApplication.delayCall += () => SaveAsset<T>(instance);
			} else{
				SaveAsset<T>(instance);
			}
#endif
			return instance;
		}


		private static void SaveAsset<T>(T obj) where T : ScriptableObject
		{
#if UNITY_EDITOR
			string dirName = "Assets/Resources";
			if(!Directory.Exists(dirName)){
				Directory.CreateDirectory(dirName);
			}
			AssetDatabase.CreateAsset(obj, "Assets/Resources/Mixpanel.asset");
			AssetDatabase.SaveAssets();
			Debug.Log("Created Mixpanel settings.");
#endif
		}
        #endregion
    }
}