using UnityEngine;

namespace mixpanel
{
    public static partial class Mixpanel
    {
        public static void Log(string s)
        {
            if (MixpanelConfig.ShowDebug)
            {
                Debug.Log("[Mixpanel] " + s);
            }
        }

        public static void LogError(string s)
        {
            if (MixpanelConfig.ShowDebug)
            {
                Debug.LogError("[Mixpanel] " + s);
            }
        }
    }
}
