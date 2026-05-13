# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the **Mixpanel Unity SDK** - an analytics library for Unity applications. It provides event tracking and user analytics capabilities through the Mixpanel platform.

**Key Characteristics:**
- Unity package distributed via Unity Package Manager (UPM)
- Supports Unity 2018.3+ (requires .NET 4.x Equivalent for older versions)
- Current version: 3.5.3
- No test suite included (test package available separately)

## Architecture

### Core Design Pattern

The SDK uses a **static singleton API** pattern with MonoBehaviour lifecycle management:

- **`Mixpanel` (static partial class)**: Public API surface in `MixpanelAPI.cs` (also Log.cs for logging)
- **`Controller` (MonoBehaviour singleton)**: Background worker managing flush intervals and initialization lifecycle
- **`MixpanelStorage` (static class)**: Persistence layer using `PlayerPreferences` (or custom `IPreferences` implementation)
- **`Value` (serializable class)**: JSON-like data structure for properties/events (see Value.cs for complex serialization logic)

### Key Components

**MixpanelAPI.cs**: Main entry point
- `Mixpanel.Track(string eventName, Value properties)` - Event tracking
- `Mixpanel.Identify(string uniqueId)` - User identification
- `Mixpanel.People.*` - User profile operations (nested static class)
- All methods check `IsInitialized()` before executing

**Controller.cs**: Lifecycle and background operations
- Auto-initializes via `[RuntimeInitializeOnLoadMethod]` unless `ManualInitialization` is enabled
- `WaitAndFlush()` coroutine periodically sends queued data based on `Config.FlushInterval`
- `DoTrack()` and `DoEngage()` are internal methods that merge properties and enqueue data

**MixpanelStorage.cs**: Data persistence
- Uses `IPreferences` interface (default: Unity's `PlayerPreferences`)
- Maintains auto-incrementing event IDs and start indices for flush optimization
- Handles migration from v1 to v2 data format

**Value.cs**: Flexible data container
- Supports primitives, arrays, objects, Unity types (Vector, Quaternion, Color, etc.)
- Custom Unity serialization via `ISerializationCallbackReceiver`
- Extensive type conversion and JSON serialization logic

**MixpanelSettings.cs**: Configuration ScriptableObject
- Managed via Unity Project Settings (Edit → Project Settings → Mixpanel)
- Separates RuntimeToken and DebugToken (DebugToken used in Editor/DEBUG builds)
- Applies settings to `Config` class at initialization

### Initialization Flow

1. **Automatic** (default): `Controller.InitializeBeforeSceneLoad()` creates singleton before first scene
2. **Manual**: Set `ManualInitialization = true` in settings, then call `Mixpanel.Init()`
3. Settings loaded from Resources via `MixpanelSettings.LoadSettings()`
4. Controller manages flush timer and auto-properties collection

### Data Flow

```
Track() → DoTrack() → Merge properties → EnqueueTrackingData() → Periodic Flush → HTTP POST
```

- Events are queued in PlayerPreferences with auto-incrementing IDs
- `Config.BatchSize` controls max events per flush (default: 50)
- `Config.FlushInterval` controls flush timing (default: 60s)
- Retry logic with exponential backoff on network failures

## Development Commands

### Package Installation

**Via Unity Package Manager (manifest.json)**:
```json
"com.mixpanel.unity": "https://github.com/mixpanel/mixpanel-unity.git#master"
```
Or point to specific version:
```json
"com.mixpanel.unity": "https://github.com/mixpanel/mixpanel-unity.git#v3.5.3"
```

**Local development** (add to `Packages/manifest.json`):
```json
"com.mixpanel.unity": "file:/path/to/cloned/mixpanel-unity"
```

### Version Management

The SDK version lives in two places that must stay in sync:
- `package.json` `version` field (UPM manifest)
- `Mixpanel/MixpanelAPI.cs:21` `MixpanelUnityVersion` constant (sent as `$lib_version` on every event)

Both are bumped automatically by the prepare-release workflow — do not edit them by hand. They are listed in `.github/modules.json` (`package_json` and `version_files`), and the publish workflow refuses to release if the two files disagree.

### Release Process

Releases follow the standardized two-step ceremony documented in the [Unity Release Runbook](https://www.notion.so/mxpnl/Unity-Release-Runbook-35ee0ba925628077baafff8a381cfe8b):

1. Run the **Prepare Release** workflow (`.github/workflows/prepare-release.yml`) from the Actions tab with `module=analytics` and the new version. It opens a release PR with version bumps, changelog, and README header updated.
2. Merge the PR, then push the tag from `master`:
   ```bash
   git checkout master && git pull
   git tag v3.5.6
   git push origin v3.5.6
   ```
3. The tag push fires `.github/workflows/release-upm.yml`, which validates the tag, gates on the `release` GitHub environment, and creates a draft GitHub release with `Examples.unitypackage` and `Tests.unitypackage` attached as assets.
4. Review the draft release on GitHub and click **Publish release**. UPM consumers install via git URL — the tag itself is the published artifact.

> **`.unitypackage` rebuild caveat:** the `Examples.unitypackage` and `Tests.unitypackage` files committed to the repo ship as-is in the GitHub release. CI does not rebuild them (Unity license required). If a release includes Examples or Tests changes, rebuild them locally with the existing scripts under `scripts/` and commit on the release prep branch before tagging.

## Code Conventions

### Namespace Structure
- Primary namespace: `mixpanel`
- Editor namespace: `mixpanel.editor`

### API Patterns
- All public API methods are static on `Mixpanel` class (partial class split across files)
- Use `IsInitialized()` guard at start of every public method
- Properties are passed via `Value` objects (dictionary-like interface)
- `Mixpanel.People.*` nested static class for user profile operations

### Unity-Specific Patterns
- Use `[RuntimeInitializeOnLoadMethod]` for initialization hooks
- Editor scripts in `Mixpanel/Editor/` with separate assembly definition
- `.meta` files must accompany all assets (auto-generated by Unity)
- Use `#if UNITY_EDITOR || DEBUG` for conditional compilation

### Documentation
- XML documentation comments (`///`) for all public APIs
- Follow existing style with `<summary>`, `<description>`, `<code>`, `<param>` tags

## Important Context

### Token Configuration
- **DebugToken**: Used in Editor or DEBUG builds
- **RuntimeToken**: Used in production builds
- Logic in `MixpanelSettings.Token` property (lines 32-40)

### Data Persistence
- Default: Unity PlayerPreferences
- Customizable via `Mixpanel.SetPreferencesSource(IPreferences)`
- Storage keys prefixed with "Mixpanel."

### Platform Considerations
- iOS-specific code guarded by `#if UNITY_IOS`
- Network reachability detection via `Application.internetReachability`
- Threading: Uses Unity coroutines, not native threads

### Migration Support
- Includes migration logic from v1 to v2 data format (`MigrateFrom1To2()`)
- Tracks migration state in PlayerPreferences

## Common Tasks

### Adding a New API Method
1. Add method to `MixpanelAPI.cs` as static method on `Mixpanel` class
2. Include `IsInitialized()` check
3. Add XML documentation
4. Call `Controller.DoTrack()` or `Controller.DoEngage()` for execution

### Modifying Default Properties
- Event properties: `Controller.GetEventsDefaultProperties()`
- People properties: `Controller.GetEngageDefaultProperties()`
- Auto-properties merged in `DoTrack()` and `DoEngage()`

### Debugging
- Enable `ShowDebug` in Project Settings
- Logs use `Mixpanel.Log()` (defined in `Log.cs`)
- Check `Config.ShowDebug` flag

### Working with Value Objects
- Supports indexer syntax: `value["key"] = "data"`
- Array operations: `value.Add(item)`, `value[index]`
- Merge operations: `value.Merge(otherValue)`
- Type conversions handled automatically

## API Reference
Full API documentation: http://mixpanel.github.io/mixpanel-unity/api-reference/annotated.html
