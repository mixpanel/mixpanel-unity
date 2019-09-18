using System;
using System.Diagnostics;
using System.IO;
using mixpanel.queue;
using UnityEngine;

namespace mixpanel
{
    public static partial class Mixpanel
    {
        #region DistinctId
        
        private const string DistinctIdName = "Mixpanel.DistinctId";
        
        private static string _distinctId;
        public static string DistinctId
        {
            get
            {
                if (!string.IsNullOrEmpty(_distinctId)) return _distinctId;
                // Generate a Unique ID for this client
                // https://devblogs.microsoft.com/oldnewthing/?p=21823
                if (!PlayerPrefs.HasKey(DistinctIdName)) DistinctId = Guid.NewGuid().ToString();
                else _distinctId = PlayerPrefs.GetString(DistinctIdName);
                return _distinctId;
            }
            set
            {
                _distinctId = value;
                PlayerPrefs.SetString(DistinctIdName, _distinctId);
            }
        }
        
        [Obsolete("Please use 'DistinctId' instead!")]
        public static string DistinctID {
            get => DistinctId;
            set => DistinctId = value;
        }
        
        #endregion

        #region IsTracking

        private const string IsTrackingName = "Mixpanel.IsTracking";
        
        private static bool _isTracking;

        public static bool IsTracking
        {
            get
            {
                if (!PlayerPrefs.HasKey(IsTrackingName)) IsTracking = true;
                else _isTracking = PlayerPrefs.GetInt(IsTrackingName) == 1;
                return _isTracking;
            }
            private set
            {
                _isTracking = value;
                PlayerPrefs.SetInt(IsTrackingName, _isTracking ? 1 : 0);
            }
        }

        #endregion

        #region PushDeviceToken

        private const string PushDeviceTokenName  = "Mixpanel.PushDeviceToken";

        private static string _pushDeviceTokenString;

        private static string PushDeviceTokenString
        {
            get
            {
                if (!string.IsNullOrEmpty(_pushDeviceTokenString)) return _pushDeviceTokenString;
                if (!PlayerPrefs.HasKey(PushDeviceTokenName)) PushDeviceTokenString = "";
                else _pushDeviceTokenString = PlayerPrefs.GetString(PushDeviceTokenName);
                return _pushDeviceTokenString;
            }
            set
            {
                _pushDeviceTokenString = value;
                PlayerPrefs.SetString(PushDeviceTokenName, _pushDeviceTokenString);
            }
        }
        
        internal static void SetPushDeviceToken(string token)
        {
            PushDeviceTokenString = token;
        }

        #endregion
        
        #region OnceProperties
        
        private const string OncePropertiesName = "Mixpanel.OnceProperties";

        private static Value _onceProperties;

        private static Value OnceProperties
        {
            get
            {
                if (_onceProperties != null) return _onceProperties;
                if (!PlayerPrefs.HasKey(OncePropertiesName)) OnceProperties = ObjectPool.Get();
                else
                {
                    _onceProperties = ObjectPool.Get();
                    JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(OncePropertiesName), _onceProperties);
                }
                return _onceProperties;
            }
            set
            {
                _onceProperties = value;
                PlayerPrefs.SetString(OncePropertiesName, JsonUtility.ToJson(_onceProperties));
            }
        }
        
        #endregion
        
        #region SuperProperties
        
        private const string SuperPropertiesName = "Mixpanel.SuperProperties";

        private static Value _superProperties;

        internal static Value SuperProperties
        {
            get
            {
                if (_superProperties != null) return _superProperties;
                if (!PlayerPrefs.HasKey(SuperPropertiesName)) SuperProperties = ObjectPool.Get();
                else
                {
                    _superProperties = ObjectPool.Get(); 
                    JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(SuperPropertiesName), _superProperties);
                }
                return _superProperties;
            }
            set
            {
                _superProperties = value;
                PlayerPrefs.SetString(SuperPropertiesName, JsonUtility.ToJson(_superProperties));
            }
        }
        
        #endregion
        
        #region TimedEvents
        
        private const string TimedEventsName = "Mixpanel.TimedEvents";

        private static Value _timedEvents;

        internal static Value TimedEvents
        {
            get
            {
                if (_timedEvents != null) return _timedEvents;
                if (!PlayerPrefs.HasKey(TimedEventsName)) TimedEvents = ObjectPool.Get();
                else 
                {
                    _timedEvents = ObjectPool.Get();
                    JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(TimedEventsName), _timedEvents);
                }
                return _timedEvents;
            }
            set
            {
                _timedEvents = value;
                PlayerPrefs.SetString(TimedEventsName, JsonUtility.ToJson(_timedEvents));
            }
        }

        #endregion

        #region TrackQueue

#if UNITY_EDITOR
        private static readonly string TrackQueueFileName = "mixpanel_track_queue" + UnityEditor.EditorUserBuildSettings.activeBuildTarget;
        private static readonly string EngageQueueFileName = "mixpanel_engage_queue" + UnityEditor.EditorUserBuildSettings.activeBuildTarget;
#else
        private static readonly string TrackQueueFileName = "mixpanel_track_queue";
        private static readonly string EngageQueueFileName = "mixpanel_engage_queue";
#endif

        public static readonly PersistentQueue TrackQueue = new PersistentQueue(Path.Combine(Application.persistentDataPath, TrackQueueFileName));

        #endregion

        #region EngageQueue

        public static readonly PersistentQueue EngageQueue = new PersistentQueue(Path.Combine(Application.persistentDataPath, EngageQueueFileName));

        #endregion
    }
}
