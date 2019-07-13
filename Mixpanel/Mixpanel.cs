using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mixpanel
{
    public static partial class Mixpanel
    {
        private const string MixpanelTrackUrl = "https://api.mixpanel.com/track";
        private const string MixpanelEngageUrl = "https://api.mixpanel.com/engage";
        
        internal static Value OnceProperties = Value.Object;
        internal static Value AutoProperties;

        internal static void DoTrack(string eventName, Value properties)
        {
            if (!IsTracking) return;
            if (AutoProperties == null) AutoProperties = CollectAutoProperties();
            properties.Merge(AutoProperties);
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

        internal static Value CollectAutoProperties()
        {
            Value properties = new Value
            {
                {"$app_build_number", Application.version},
                {"$app_version", Application.unityVersion},
                {"$device", Application.platform.ToString()},
                {"$model", SystemInfo.deviceModel},
                {"$os", SystemInfo.operatingSystemFamily.ToString()},
                {"$os_version", SystemInfo.operatingSystem},
                {"$screen_dpi", Screen.dpi},
                {"$wifi", Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork},
                {"mp_lib", "unity"}
            };
            return properties;
        }

        internal static void DoEngage(Value data)
        {
            if (!IsTracking) return;
            data["$token"] = MixpanelSettings.Instance.Token;
            data["$distinct_id"] = DistinctId;
            Enqueue(MixpanelEngageUrl, data);
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
