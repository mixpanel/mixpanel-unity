using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace mixpanel
{
    public static partial class Mixpanel
    {
        private const string MixpanelUnityVersion = "2.0.0";
        
        private const string MixpanelTrackUrl = "https://api.mixpanel.com/track";
        private const string MixpanelEngageUrl = "https://api.mixpanel.com/engage";
        
        internal static Value OnceProperties = Value.Object;
        internal static Value AutoTrackProperties;
        internal static Value AutoEngageProperties;

        internal static void DoTrack(string eventName, Value properties)
        {
            if (!IsTracking) return;
            if (AutoTrackProperties == null) AutoTrackProperties = CollectAutoTrackProperties();
            properties.Merge(AutoTrackProperties);
            // These auto properties can change in runtime so we don't bake them into AutoProperties
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            properties.Merge(OnceProperties);
            OnceProperties = new Value();
            properties.Merge(SuperProperties);
            if (TimedEvents.TryGetValue(eventName, out Value startTime))
            {
                properties["$duration"] = CurrentTime() - (double)startTime;
                TimedEvents.Remove(eventName);
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = DistinctId;
            properties["time"] = CurrentTime();
            Value data = new Value { {"event", eventName}, {"properties", properties} };
            Enqueue(MixpanelTrackUrl, data);
        }

        internal static Value CollectAutoTrackProperties()
        {
            Value properties = new Value
            {
                {"mp_lib", "unity"},
                {"$lib_version", MixpanelUnityVersion},
                {"$os", SystemInfo.operatingSystemFamily.ToString()},
                {"$os_version", SystemInfo.operatingSystem},
                {"$manufacturer", ""},
                {"$model", SystemInfo.deviceModel},
                {"$app_version_string", Application.unityVersion},
                {"$app_build_number", Application.version},
                {"$carrier", ""},
                {"$wifi", Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork},
                {"$radio", GetRadio()},
                {"$brand", ""},
                {"$device", Application.platform.ToString()},
                {"$screen_dpi", Screen.dpi},
                {"$has_nfc", false},
                {"$has_telephone", false},
                {"$bluetooth_enabled", false},
                {"$bluetooth_version", "none"}
            };
            #if UNITY_IOS
            properties["$os_version"] = Device.systemVersion},
            properties["$manufacturer"] = "Apple";
            properties["$ios_ifa"] = Device.advertisingIdentifier;
            #endif
            #if UNITY_ANDRIOD
            properties["$google_play_services"] = "";
            #endif
            return properties;
        }

        internal static void DoEngage(Value properties)
        {
            if (!IsTracking) return;
            if (AutoEngageProperties == null) AutoEngageProperties = CollectAutoEngageProperties();
            properties.Merge(AutoEngageProperties);
            properties["$token"] = MixpanelSettings.Instance.Token;
            properties["$distinct_id"] = DistinctId;
            Enqueue(MixpanelEngageUrl, properties);
        }
        
        internal static Value CollectAutoEngageProperties()
        {
            Value properties = new Value();
            #if UNITY_IOS
            properties["$ios_lib_version"] = MixpanelUnityVersion;
            properties["$ios_version"] = Device.systemVersion;
            properties["$ios_app_version"] = Application.version;
            properties["$ios_app_release"] = Application.unityVersion;
            properties["$ios_device_model"] = SystemInfo.deviceModel;
            properties["$ios_ifa"] = Device.advertisingIdentifier;
            #endif
            #if UNITY_ANDRIOD
            properties["$android_lib_version"] = MixpanelUnityVersion;
            properties["$android_os"] = "Android";
            properties["$android_os_version"] = SystemInfo.operatingSystem;
            properties["$android_manufacturer"] = "";
            properties["$android_brand"] = "";
            properties["$android_model"] = SystemInfo.deviceModel;
            properties["$android_app_version"] = Application.unityVersion;
            properties["$android_app_version_code"] = Application.version;
            #endif
            return properties;
        }

        internal static string GetRadio()
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

        internal static double CurrentTime()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double currentEpochTime = (DateTime.UtcNow - epochStart).TotalSeconds;
            return currentEpochTime;
        }

        internal static string CurrentDateTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        }

        internal static void Load()
        {
            LoadData();
            LoadBatches();
        }

        internal static void Save()
        {
            SaveData();
            SaveBatches();
        }
    }
}
