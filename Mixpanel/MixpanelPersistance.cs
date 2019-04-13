using UnityEngine;

namespace mixpanel
{
    public static partial class Mixpanel
    {
        internal const string DistinctIDName = "Mixpanel.DistinctID";
        internal const string IsTrackingName = "Mixpanel.IsTracking";
        internal const string SuperPropertiesName = "Mixpanel.SuperProperties";
        internal const string TimedEventsName = "Mixpanel.TimedEvents";

        public static string DistinctID {
            get {
                if (!PlayerPrefs.HasKey(DistinctIDName))
                {
                    PlayerPrefs.SetString(DistinctIDName, GetID());
                }
                return PlayerPrefs.GetString(DistinctIDName);
            }
            set {
                PlayerPrefs.SetString(DistinctIDName, value);
            }
        }

        public static bool IsTracking {
            get {
                if (!PlayerPrefs.HasKey(IsTrackingName))
                {
                    PlayerPrefs.SetInt(IsTrackingName, 1);
                }
                return PlayerPrefs.GetInt(IsTrackingName) == 1 ? true : false;
            }
            set {
                PlayerPrefs.SetInt(IsTrackingName, value ? 1 : 0);
            }
        }

        internal static Value SuperProperties {
            get {
                if (!PlayerPrefs.HasKey(SuperPropertiesName))
                {
                    PlayerPrefs.SetString(SuperPropertiesName, Json.Serialize(new Value()));
                }
                return Json.Deserialize(PlayerPrefs.GetString(SuperPropertiesName));
            }
            set {
                PlayerPrefs.SetString(SuperPropertiesName, Json.Serialize(value));
            }
        }

        internal static Value TimedEvents {
            get {
                if (!PlayerPrefs.HasKey(TimedEventsName))
                {
                    PlayerPrefs.SetString(TimedEventsName, Json.Serialize(new Value()));
                }
                return Json.Deserialize(PlayerPrefs.GetString(TimedEventsName));
            }
            set {
                PlayerPrefs.SetString(TimedEventsName, Json.Serialize(value));
            }
        }
    }
}
