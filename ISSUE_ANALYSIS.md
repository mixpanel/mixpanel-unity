# Event Volume Drop and Missing Super Properties After Unity SDK Migration

## Issue Summary
After migrating from native SDKs (Android 5.3.0 and custom iOS wrapper) to mixpanel-unity v3.5.3, customers are experiencing:

1. **30-44% drop in event volume** for App Start and App Exit events on Android (Google Play) only
2. **Missing super properties** on the first event fired in each session (App Start event) on both iOS and Android
3. Events fired between App Start and App Exit work normally with super properties present

## Environment
- **SDK Version**: mixpanel-unity v3.5.3
- **Platform**: Android (Google Play) - event volume drop; Both iOS and Android - missing super properties
- **Migration**: From Android SDK 5.3.0 (Gradle) and iOS custom wrapper (commit 0b982aab58e832ad85a8d9dca4d9729aa3948b53)
- **Unity Version**: Not specified
- **Deployment**: Production environment

## Affected Games
- Sago Mini School (rollout Oct 1st, 2025)
- Sago Mini World (also released with SDK)
- Project: Sago Mini - Production

## Detailed Symptoms

### 1. Event Volume Drop (Android Only)
- **Metric**: ~30% drop in total events, ~44% drop in unique users for App Start and App Exit
- **Comparison**: Sep 30th vs 30 days prior (pre-migration baseline)
- **Pattern**: 
  - App Start events: Significantly reduced
  - App Exit events: Significantly reduced  
  - Mid-session events: Normal volume
- **iOS**: No event volume drop observed
- **Dashboard**: https://mixpanel.com/project/210340/view/23451/app/insights#fTLhAYFBMnRZ

### 2. Missing Super Properties (iOS + Android)
- **Scope**: First event of every session (App Start event)
- **Behavior**: Super properties that should be present are completely missing
- **Registration**: Super property registration code executes at same location as before migration
- **Later Events**: All events after the first event in a session include super properties correctly

### 3. Not Reproducible Locally
- Issue only manifests in production with real users
- Local testing (download from Play Store, test locally) does not reproduce the issue
- This suggests timing-related issue or device-specific behavior

## Customer's Investigation

### What They Checked
- ✅ Initialization paths appear to run (Mixpanel.Init + subsequent calls)
- ✅ Super property registration code still executes (same location as before)
- ✅ No recent logic changes that would gate or suppress tracking
- ✅ No obvious token or environment mismatches
- ✅ DAU metrics from App Store/Play Store show no actual user drop

### What They Observed
- Migration kept existing C# wrapper/util classes
- Only swapped underlying calls to Unity Mixpanel API
- Games not yet migrated continue to report expected volumes with super properties present

### Code Flow
Customer provided sequence diagram showing:
1. Bootstrap scene loads
2. Mixpanel.Init() called early in first scene
3. App Start event fired immediately after initialization
4. Super property registration happens in their SagoMixpanelBinding wrapper

## Technical Analysis (Mixpanel Support)

### Lin Yee Koh (Support)
> "Mixpanel Unity (v3.5.3) initializes asynchronously. It is possible that the first event can fire before the instance is ready or before the super-properties file is loaded from disk."

### Customer Response (Guilherme)
> "Hello, but the Mixpanel.Init() call seems sync. It is not a coroutine or Task method. It just applies the configs from the scriptable object and then initialize a singleton instance. How can we wait for the initialization finish? I didn't find an event or something that would tell the call finished."

### App Exit Event Mystery
Customer noted that missing App Exit events is harder to explain with the async initialization theory:
> "That still doesn't explain the missing App Exit events... Any queued or cached App Exit should get fired in the next launch as a flush() called automatically every 60 seconds."

## Root Cause (Identified)

After investigation, the root cause is a **race condition during initialization**:

### 1. Lazy Property Loading
- Super properties are stored in PlayerPreferences/disk
- Properties are lazily loaded on first access via `MixpanelStorage.SuperProperties` getter
- On Android, PlayerPreferences read operations can be slow, especially on first access after app launch
- If `Track()` is called immediately after `Init()`, properties may not be loaded yet

### 2. Auto Properties Not Cached Early
- `GetEventsDefaultProperties()` and `GetEngageDefaultProperties()` cache platform-specific auto properties
- These were called in `InitializeAfterSceneLoad()` (RuntimeInitializeLoadType.AfterSceneLoad)
- If `Track()` is called during or before AfterSceneLoad, auto properties aren't cached yet
- Each uncached call would regenerate them (performance issue + timing issue)

### 3. Session Metadata Not Initialized
- Session metadata (`_sessionID`, counters, etc.) was only initialized in `OnApplicationPause()`
- First event could have null/uninitialized session metadata

### 4. App Exit Event Loss
- App Exit events queued to PlayerPreferences
- Auto-flush happens every 60 seconds
- On Android, aggressive process killing means:
  - App Exit event queued but not flushed before process killed
  - Event should send on next launch, but if there are issues with loading queued events from PlayerPreferences, they could be lost

## Solution Implemented

Modified `Controller.Initialize()` to **eagerly load all properties synchronously**:

```csharp
internal static void Initialize() {
    MixpanelSettings.Instance.ApplyToConfig();
    GetInstance();
    
    // Eagerly load all persisted properties to ensure they're available immediately
    var _ = MixpanelStorage.SuperProperties;
    var __ = MixpanelStorage.OnceProperties;
    var ___ = MixpanelStorage.TimedEvents;
    
    // Pre-cache auto properties
    GetEventsDefaultProperties();
    GetEngageDefaultProperties();
    
    // Initialize session metadata
    Metadata.InitSession();
}
```

### Benefits
1. **Synchronous Loading**: All properties loaded from disk during `Init()` before it returns
2. **No Race Condition**: `Track()` can be called immediately after `Init()` with properties guaranteed to be available
3. **Session Metadata Ready**: Session ID and counters initialized before first event
4. **Performance**: Auto properties cached once instead of regenerating

## Testing Recommendations

### For Customers
1. Deploy updated SDK to staging environment
2. Test immediate tracking after Init():
   ```csharp
   Mixpanel.Init();
   Mixpanel.Register("test_property", "test_value");
   Mixpanel.Track("App Start"); // Should include test_property
   ```
3. Verify super properties present on first event in production
4. Monitor event volumes for 1-2 weeks post-deployment
5. Compare App Start/Exit event volumes to pre-migration baseline

### For Mixpanel QA
1. Test on low-end Android devices (slower storage I/O)
2. Test with large super property sets (slower loading)
3. Test rapid Init() → Track() sequences
4. Verify session metadata present on first event
5. Test App Exit event flush behavior

## References
- **Slack Thread**: #mixpanel-sagomini, Nov 5-19, 2025
- **Zendesk Case**: #674175
- **Customer**: Sago Mini (Piknik)
- **Project**: Sago Mini - Production (ID: 210340)
- **Dashboard**: https://mixpanel.com/project/210340/view/23451/app/insights#fTLhAYFBMnRZ

## Related Code Files
- `Mixpanel/Controller.cs` - Initialization logic
- `Mixpanel/Storage.cs` - Property persistence and lazy loading
- `Mixpanel/MixpanelAPI.cs` - Public API surface

## Timeline
- **Oct 1, 2025**: Customer rolled out Unity SDK migration to production
- **Nov 5, 2025**: Customer reported issue to Mixpanel
- **Nov 10, 2025**: Escalated to Senior Support Engineer
- **Nov 18, 2025**: Mobile Engineering team engaged (Jared McFarland)
- **Nov 19, 2025**: Root cause identified, fix in progress
- **Nov 20, 2025**: Fix implemented and tested
