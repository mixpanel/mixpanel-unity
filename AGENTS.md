# AGENTS.md

Guidance for OpenAI Codex coding agents working on the Mixpanel Unity SDK. Keep this current so agents can ramp quickly.

## Mission & Constraints
- Maintain and extend the Unity SDK under `Mixpanel/` (plus assets). Package ships via Unity Package Manager; treat `.meta` files as required artifacts.
- Target Unity 2018.3+ with .NET 4.x Equivalent. Guard any newer API usage.
- No test suite lives here (tests are in `Tests.unitypackage`). Expect manual validation or targeted scripts.
- Unity threading model only—work through coroutines, not raw threads.

## Core Architecture (pair with `.github/copilot-instructions.md`)
- `Mixpanel` static partial class (`MixpanelAPI.cs`, `Log.cs`, etc.) is the public API. Every public method starts with `IsInitialized()` and then delegates to controller helpers.
- `Controller` MonoBehaviour singleton (`Controller.cs`) governs initialization, coroutine-based flush cadence, default property capture, and migration hooks.
- `MixpanelStorage` (`Storage.cs`) uses `IPreferences` (default `PlayerPreferences`) for persistence. Watch key names and migration flags (`HasMigratedFrom1To2`).
- `Value` (`Value.cs`) is the JSON-like container for event/people properties (primitives, arrays, Unity structs like Vector/Quaternion/Color) with merge and serialization logic.
- `MixpanelSettings` + `Config` surface tokens, debug flags, flush/batch settings, and manual initialization. Token selection follows:
  ```csharp
  #if UNITY_EDITOR || DEBUG
      return DebugToken;
  #else
      return RuntimeToken;
  #endif
  ```

## Development Workflow
1. **Version bumps**: use `python scripts/release.py --old X.Y.Z --new A.B.C` to automate version updates, commit, tag, and push. Manual alternative: update `MixpanelAPI.cs` (`MixpanelUnityVersion`), `package.json`, and `CHANGELOG.md`; then tag (`git tag -a vX.Y.Z -m "version X.Y.Z" && git push origin --tags`).
2. **Local testing**: in a Unity project `Packages/manifest.json`, add `"com.mixpanel.unity": "file:/absolute/path/to/mixpanel-unity"`. Import `Examples.unitypackage` for sample scenes.
3. **Debugging**: enable `ShowDebug` in Unity Project Settings → Mixpanel or call `Mixpanel.Log()` (respects `Config.ShowDebug`).
4. **PR hygiene**: ensure every new asset/code file has a `.meta`. Do not delete existing `.meta` files; Unity regenerates them if needed.

## Implementation Patterns
- Auto-init: `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`; respect `Config.ManualInitialization` and the `Mixpanel.Init()` path.
- Event flow: `Track()` → `DoTrack()` → merge default + user props → `EnqueueTrackingData()` → periodic flush (default 60s, configurable). People ops use `DoEngage()`.
- Tuning: `Config.BatchSize` and `Config.FlushInterval`. When changing defaults, update docs and any editor UI.
- Serialization changes in `Value` must update the backing store and JSON conversion and stay backward compatible with existing PlayerPreferences data.

## Coding Guidelines
- Namespaces: `mixpanel` (runtime) and `mixpanel.editor` (editor scripts). Keep assembly definition boundaries intact.
- Public APIs need XML docs (`<summary>`, `<param>`). Match existing tone.
- Conditional compilation: `#if UNITY_EDITOR || DEBUG` for editor-only logic; platform guards like `#if UNITY_IOS` where applicable.
- Keep dependencies minimal to stay UPM-friendly.

## Practical Tips
- Use `rg` for search (`rg SymbolName Mixpanel`) to navigate the codebase quickly.
- When editing storage, migrations, or PlayerPreferences keys, add concise inline comments for future debugging.
- Prefer placing sample/debug scripts under `Mixpanel/Examples` and referencing them in docs rather than leaving ad-hoc files at repo root.

## Quick Reference
- Docs: `README.md`, `CLAUDE.md`, `.github/copilot-instructions.md`
- Release automation: `.github/workflows/`
- Support links: README FAQ and Mixpanel support portal

Update this playbook whenever workflows or expectations change.
