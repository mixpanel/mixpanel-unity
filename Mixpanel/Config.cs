namespace mixpanel
{
    internal static class Config
    {
        // Can be overriden by MixpanelSettings
        internal static string TrackUrl = "https://api.mixpanel.com/track/?ip=1";
        internal static string EngageUrl = "https://api.mixpanel.com/engage/?ip=1";
        internal static string GroupsUrl = "https://api.mixpanel.com/groups/?ip=1";
        internal static bool ShowDebug = false;
        internal static bool ManualInitialization = false;
        internal static float FlushInterval = 60f;
        // Fixed once Mixpanel is initialized; see MixpanelSettings.HierarchicalGroups
        internal static bool HierarchicalGroups = false;

        internal static int BatchSize = 50;
    }
}