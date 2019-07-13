using System;
using UnityEngine;

namespace mixpanel
{
    public static partial class Mixpanel
    {
        private const string DistinctIdName = "Mixpanel.DistinctId";
        private const string IsTrackingName = "Mixpanel.IsTracking";
        private const string SuperPropertiesName = "Mixpanel.SuperProperties";
        private const string TimedEventsName = "Mixpanel.TimedEvents";

        public static string DistinctId;
        [Obsolete("Please use 'DistinctId' instead!")]
        public static string DistinctID {
            get => DistinctId;
            set => DistinctId = value;
        }

        public static bool IsTracking;
        internal static Value SuperProperties;
        internal static Value TimedEvents;

        internal static void LoadData()
        {
            if (!PlayerPrefs.HasKey(DistinctIdName))
            {
                // Generate a Unique ID for this client
                // https://devblogs.microsoft.com/oldnewthing/?p=21823
                PlayerPrefs.SetString(DistinctIdName, Guid.NewGuid().ToString());
            }
            DistinctId = PlayerPrefs.GetString(DistinctIdName);
            
            if (!PlayerPrefs.HasKey(IsTrackingName))
            {
                PlayerPrefs.SetInt(IsTrackingName, 1);
            }
            IsTracking = PlayerPrefs.GetInt(IsTrackingName) == 1;
            
            if (!PlayerPrefs.HasKey(SuperPropertiesName))
            {
                PlayerPrefs.SetString(SuperPropertiesName, Value.Object.Serialize());
            }
            SuperProperties = Value.Deserialize(PlayerPrefs.GetString(SuperPropertiesName));
            
            if (!PlayerPrefs.HasKey(TimedEventsName))
            {
                PlayerPrefs.SetString(TimedEventsName, Value.Object.Serialize());
            }
            TimedEvents = Value.Deserialize(PlayerPrefs.GetString(TimedEventsName));
        }

        internal static void SaveData()
        {
            PlayerPrefs.SetString(DistinctIdName, DistinctId);
            PlayerPrefs.SetInt(IsTrackingName, IsTracking ? 1 : 0);
            PlayerPrefs.SetString(SuperPropertiesName, SuperProperties.Serialize());
            PlayerPrefs.SetString(TimedEventsName, TimedEvents.Serialize());
        }
    }
}
