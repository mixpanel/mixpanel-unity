# AGENTS.md

Guidance for OpenAI Codex coding agents collaborating on the Mixpanel Unity SDK. Keep this file up to date so future agents can ramp quickly.

## Mission & Constraints
- Primary goal: maintain and extend the Unity SDK located in this repo (`Mixpanel/` plus associated assets). The package ships through Unity Package Manager; treat `.meta` files as first-class citizens.
- Unity target: 2018.3+ with .NET 4.x Equivalent. Avoid API usages newer than that unless you guard them.
- No automated tests live in the repo. Separate `Tests.unitypackage` exists, so changes generally require manual validation or targeted scripts.
- Network calls and platform-specific code must stay within Unity’s coroutine/threading model (no raw threads).

## Key Architecture (read with `.github/copilot-instructions.md`)
- `Mixpanel` static partial class (`MixpanelAPI.cs`, `Log.cs`, etc.) exposes the public surface. Every new public method must guard with `IsInitialized()`.
- `Controller` MonoBehaviour singleton (`Controller.cs`) manages initialization, periodic flushing, default property collection, and migration hooks.
- `MixpanelStorage` (`Storage.cs`) reads/writes data via `IPreferences` (default `PlayerPreferences`). Be careful with key naming and migrations (`HasMigratedFrom1To2`).
- `Value` (`Value.cs`) is the JSON-like container used for event/user properties. It supports primitives, arrays, Unity structs (Vector, Quaternion, Color), and merge operations.
- `MixpanelSettings` + `Config` control tokens, debug flags, flush/batch values, and manual initialization. Tokens follow the Debug vs Runtime pattern:
  ```csharp
  #if UNITY_EDITOR || DEBUG
      return DebugToken;
  #else
      return RuntimeToken;
  #endif
  ```

## Development Workflow
1. **Version bumps**: update `MixpanelAPI.cs (MixpanelUnityVersion)`, `package.json`, and `CHANGELOG.md`, then tag (`git tag vX.Y.Z && git push origin vX.Y.Z`).
2. **Local testing**: add `"com.mixpanel.unity": "file:/absolute/path/to/mixpanel-unity"` to a Unity project’s `Packages/manifest.json`. Import `Examples.unitypackage` for manual validation scenarios.
3. **Debugging**: enable `ShowDebug` in Unity Project Settings → Mixpanel or use `Mixpanel.Log()` (respects `Config.ShowDebug`).
4. **Pull Requests**: ensure new files include `.meta` counterparts (Unity generates them, but don’t delete existing ones).

## Implementation Patterns
- Auto-init occurs via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`; respect `Config.ManualInitialization` for manual flows (`Mixpanel.Init()` path).
- Event flow: `Track()` → `DoTrack()` → merge default + user props → `EnqueueTrackingData()` → periodic flush (60s default, configurable). People updates go through `DoEngage()`.
- Batch/flush tuning: `Config.BatchSize` and `Config.FlushInterval`. If you change defaults, update documentation and settings UI.
- For new serialization cases in `Value`, extend both the storage dictionary logic and JSON conversion. Preserve backward compatibility in PlayerPreferences storage.

## Coding Guidelines
- Namespace: `mixpanel` (runtime) and `mixpanel.editor` (editor scripts). Maintain existing assembly definition setups.
- Documentation: public APIs need XML docs with `<summary>` and `<param>` tags. Keep tone consistent with current files.
- Conditional compilation: use `#if UNITY_EDITOR || DEBUG` for editor-only logic and guards like `#if UNITY_IOS` for platform-specific code.
- Avoid new dependencies without coordination; Unity packages must stay lightweight.

## Practical Tips
- Use `rg` for searches (`rg SymbolName Mixpanel`); `git grep` is slower.
- When touching serialization, migration, or PlayerPreferences keys, add inline comments explaining decisions—future contributors rely on them during debugging.
- If you need test scaffolding inside Unity, prefer sample scripts placed under `Mixpanel/Examples` and reference them in documentation rather than leaving ad-hoc files in root.

## Quick Reference
- Docs: README.md, CLAUDE.md, `.github/copilot-instructions.md`
- Release automation: `.github/workflows/*`
- Support links: README’s FAQ + Mixpanel support portal

Keep this document concise but thorough; update it whenever workflows or expectations evolve.
