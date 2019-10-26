using System;
using UnityEngine;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace mixpanel
{
    public static partial class Mixpanel
    {
        private const string MixpanelUnityVersion = "2.0.0";
        
        private static Value _autoTrackProperties;
        private static Value _autoEngageProperties;
        private static Int32 _eventCounter = 0, _peopleCounter = 0, _sessionStartEpoch;
        private static String _sessionID;

        private static Value GetEventMetadata() {
            Value eventMetadata = new Value
            {
                {"$mp_event_id", Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16)},
                {"$mp_session_id", _sessionID},
                {"$mp_session_seq_id", _eventCounter},
                {"$mp_session_start_sec", _sessionStartEpoch}
            };
            _eventCounter++;
            return eventMetadata;
        }

        private static Value GetPeopleMetadata() {
            Value peopleMetadata = new Value
            {
                {"$mp_event_id", Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16)},
                {"$mp_session_id", _sessionID},
                {"$mp_session_seq_id", _peopleCounter},
                {"$mp_session_start_sec", _sessionStartEpoch}
            };
            _peopleCounter++;
            return peopleMetadata;
        }

        internal static void InitSession() {
            _eventCounter = 0;
            _peopleCounter = 0;
            _sessionID = Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16);
            _sessionStartEpoch = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private static void DoTrack(string eventName, Value properties)
        {
            if (!IsTracking) return;
            if (properties == null) properties = ObjectPool.Get();
            properties.Merge(GetEventsDefaultProperties());
            // These auto properties can change in runtime so we don't bake them into AutoProperties
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            properties.Merge(OnceProperties);
            ResetOnceProperties();
            properties.Merge(SuperProperties);
            if (TimedEvents.TryGetValue(eventName, out Value startTime))
            {
                properties["$duration"] = CurrentTime() - (double)startTime;
                TimedEvents.Remove(eventName);
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = DistinctId;
            properties["time"] = CurrentTime();
            Value data = ObjectPool.Get();
            data["event"] = eventName;
            data["properties"] = properties;
            data["$mp_metadata"] = GetEventMetadata();
            MixpanelManager.EnqueueTrack(data);
        }

        private static void DoEngage(Value properties)
        {
            if (!IsTracking) return;
            properties["$token"] = MixpanelSettings.Instance.Token;
            properties["$distinct_id"] = DistinctId;
            properties["$time"] = CurrentTime();
            properties["$mp_metadata"] = GetPeopleMetadata();
            MixpanelManager.EnqueueEngage(properties);
        }

        internal static void CollectAutoProperties()
        {
            GetEngageDefaultProperties();
            GetEventsDefaultProperties();
        }

        internal static Value GetEngageDefaultProperties() {
            if (_autoEngageProperties == null) {
                Value properties = new Value();
                    #if UNITY_IOS
                        properties["$ios_lib_version"] = MixpanelUnityVersion;
                        properties["$ios_version"] = Device.systemVersion;
                        properties["$ios_app_release"] = Application.version;
                        properties["$ios_device_model"] = SystemInfo.deviceModel;
                        properties["$ios_ifa"] = Device.advertisingIdentifier;
                        // properties["$ios_app_version"] = Application.version;
                    #elif UNITY_ANDROID
                        properties["$android_lib_version"] = MixpanelUnityVersion;
                        properties["$android_os"] = "Android";
                        properties["$android_os_version"] = SystemInfo.operatingSystem;
                        properties["$android_model"] = SystemInfo.deviceModel;
                        properties["$android_app_version"] = Application.version;
                        AndroidJavaClass osBuildClass = new AndroidJavaClass("android.os.Build");
                        string manufacturer = osBuildClass.GetStatic<string> ("MANUFACTURER");
                        string brand = osBuildClass.GetStatic<string> ("BRAND");
                        properties["$android_manufacturer"] = manufacturer != null? manufacturer : "UNKNOWN";
                        properties["$android_brand"] = brand != null ? brand : "UNKNOWN";
                        // properties["$android_app_version_code"] = Application.version;
                    #else
                        properties["$lib_version"] = MixpanelUnityVersion;
                    #endif
                _autoEngageProperties = properties;
            }
            return _autoEngageProperties;
        }

        private static Value GetEventsDefaultProperties()
        {
            if (_autoTrackProperties == null) {
                Value properties = new Value
                {
                    {"mp_lib", "unity"},
                    {"$lib_version", MixpanelUnityVersion},
                    {"$os", SystemInfo.operatingSystemFamily.ToString()},
                    {"$os_version", SystemInfo.operatingSystem},
                    {"$model", SystemInfo.deviceModel},
                    {"$app_version_string", Application.version},
                    {"$wifi", Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork},
                    {"$radio", GetRadio()},
                    {"$device", Application.platform.ToString()},
                    {"$screen_dpi", Screen.dpi},
                    // {"$app_build_number", Application.version},
                    // {"$carrier", ""},
                    // {"$has_nfc", false},
                    // {"$has_telephone", false},
                    // {"$bluetooth_enabled", false},
                    // {"$bluetooth_version", "none"}
                };
                #if UNITY_IOS
                    properties["$os"] = "Apple";
                    properties["$os_version"] = Device.systemVersion;
                    properties["$manufacturer"] = "Apple";
                    properties["$ios_ifa"] = Device.advertisingIdentifier;
                #endif
                #if UNITY_ANDROID
                    AndroidJavaClass osBuildClass = new AndroidJavaClass("android.os.Build");
                    string manufacturer = osBuildClass.GetStatic<string> ("MANUFACTURER");
                    string brand = osBuildClass.GetStatic<string> ("BRAND");
                    properties["$manufacturer"] = manufacturer != null? manufacturer : "UNKNOWN";
                    properties["$brand"] = brand != null ? brand : "UNKNOWN";
                    properties["$os"] = "Android";
                    // properties["$google_play_services"] = "";
                #endif
                _autoTrackProperties = properties;
            }
            return _autoTrackProperties;
        }

        private static string GetRadio()
        {
            switch(Application.internetReachability)
            {
                case NetworkReachability.NotReachable :
                    return "none";
                case NetworkReachability.ReachableViaCarrierDataNetwork :
                    return "carrier";
                case NetworkReachability.ReachableViaLocalAreaNetwork :
                    return "wifi";
            }
            return "none";
        }

        private static double CurrentTime()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double currentEpochTime = (DateTime.UtcNow - epochStart).TotalSeconds;
            return currentEpochTime;
        }

        private static string CurrentDateTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }
}
