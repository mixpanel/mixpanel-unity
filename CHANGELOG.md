#

## [v3.1.1](https://github.com/mixpanel/mixpanel-unity/tree/v3.1.1) (2022-04-30)

### Enhancements

- Add Debug tracking and Dev NPS Survey [\#138](https://github.com/mixpanel/mixpanel-unity/pull/138)

### Fixes

- Remove push notification related stuff [\#137](https://github.com/mixpanel/mixpanel-unity/pull/137)

#

## [v3.1.0](https://github.com/mixpanel/mixpanel-unity/tree/v3.1.0) (2022-04-21)

### Enhancements

- Customize player preferences [\#128](https://github.com/mixpanel/mixpanel-unity/pull/128)

### Fixes

- Fix compilation errors on platforms other than Android [\#135](https://github.com/mixpanel/mixpanel-unity/pull/135)

#

## [v3.0.2](https://github.com/mixpanel/mixpanel-unity/tree/v3.0.2) (2022-01-14)

### Fixes

- Fix the Mixpanel gameObject could potentially being destroyed when switch between scenes [\#125](https://github.com/mixpanel/mixpanel-unity/pull/125)

#

## [v3.0.1](https://github.com/mixpanel/mixpanel-unity/tree/v3.0.1) (2021-11-11)

**Closed issues:**

- Error with Controller.cs with earlier unity versions \( 2019.4\) [\#120](https://github.com/mixpanel/mixpanel-unity/issues/120)

**Merged pull requests:**

- backward compatibility  with earlier version than 2020.1 [\#121](https://github.com/mixpanel/mixpanel-unity/pull/121)

#

## [v3.0.0](https://github.com/mixpanel/mixpanel-unity/tree/v3.0.0) (2021-10-21)

### Enhancements

- Android Push Notifications  [\#42](https://github.com/mixpanel/mixpanel-unity/issues/42)
- Tracking persistence layer refactor [\#119](https://github.com/mixpanel/mixpanel-unity/pull/119)

### Fixes

- `OnApplicationQuit` possible race condition with tracking [\#69](https://github.com/mixpanel/mixpanel-unity/issues/69)

**Closed issues:**

- Expose Mixpanel.ClearSuperProperties? [\#118](https://github.com/mixpanel/mixpanel-unity/issues/118)
- Mixpanel doesn't work with IL2CPP build on windows.  [\#117](https://github.com/mixpanel/mixpanel-unity/issues/117)
- TrackCharge  doc is out of date? [\#116](https://github.com/mixpanel/mixpanel-unity/issues/116)
- IL2CPP error building for Android in Unity 2020.2.2f1 [\#112](https://github.com/mixpanel/mixpanel-unity/issues/112)
- v2.2.2 still referencing IDFA [\#111](https://github.com/mixpanel/mixpanel-unity/issues/111)
- Mixpanel prevents opening the same Unity project open twice [\#110](https://github.com/mixpanel/mixpanel-unity/issues/110)
- Standalone builds\(IL2CPP\) won't send Mixpanel events [\#108](https://github.com/mixpanel/mixpanel-unity/issues/108)
- \[Mixpanel\] There was an error sending the request. System.AggregateException: One or more errors occurred. [\#106](https://github.com/mixpanel/mixpanel-unity/issues/106)
- \[Mixpanel\] System.NotSupportedException: linked away [\#105](https://github.com/mixpanel/mixpanel-unity/issues/105)
- WebGL freezing on v2.1.0 [\#90](https://github.com/mixpanel/mixpanel-unity/issues/90)
- NullReferenceException on low performance devices [\#89](https://github.com/mixpanel/mixpanel-unity/issues/89)
- JSON parse error on empty object [\#87](https://github.com/mixpanel/mixpanel-unity/issues/87)
- End of file reached while trying to read queue item on UWP [\#85](https://github.com/mixpanel/mixpanel-unity/issues/85)

**Merged pull requests:**

- Improve README for quick start guide [\#115](https://github.com/mixpanel/mixpanel-unity/pull/115)
- Add github workflow for auto release [\#114](https://github.com/mixpanel/mixpanel-unity/pull/114)

## [v2.2.3](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.2.3)
### Mar 8 - 2020
## Fixes
- Remove `$ios_ifa`

---

## [v2.2.2](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.2.2)
### October 26 - 2020

## Fixes
- Fix in some rare cases, event payload being sent incorrectly formatted or with changed values

---

## [v2.2.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.2.1)
### July 31st - 2020

- Remove `$ios_ifa` user property for iOS devices: iOS 14 will not allow to read the IDFA value without permission.

## Fixes
- Improve objects re-utilization.

---

## [v2.2.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.2.0)
### June 2nd - 2020

## Features
- You can now manually initialize the library. You first need to enable this setting 
from your Project Settings menu. To use the library, call `Mixpanel.Init()` before you interact with it 
and `Mixpanel.Disable()` to dispose the component. 

## Fixes
- Fix fatal errror in `Mixpanel.Reset()` at app boot (thanks @RedHatJef!)

---

## [v2.1.4](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.1.4)
### February 18th - 2020

## Fixes
- Performance improvements.
- Fix set `PushDeviceToken` for Android where an string is used.

---

## [v2.1.3](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.1.3)
### February 10th - 2020

## Fixes
- Remove `ClearCharges` from `OptOutTracking` to avoid having orphan profiles at mixpanel

---

## [v2.1.2](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.1.2)
### January 9th - 2020

## New features
- Add `SetToken()` method to set project token programatically

---

## [v2.1.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.1.1)
### December 17th - 2019

## Fixes
- Added support for older Unity versions if .NET 4.x equivalent is the selected scripting runtime version
- Fix value serialize/deserialize bug (#93)

---

## [v2.1.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.1.0)
### November 14th - 2019

## Fixes
- API Error: Invalid JSON Encoding for numbers (https://github.com/mixpanel/mixpanel-unity/issues/74)
- Default people properties not been set properly
- `PushDeviceToken` not working (https://github.com/mixpanel/mixpanel-unity/issues/73)
- JSON encoding of special characters like `\"` or `\t`, etc...
- A flush operation now sends everything that happened until just right before the API is called.
- Properly migrate state from SDK 1.X to 2.X to preserve super properties and distinct ids.
- Major performance improvements

## New features
- Added de-duplication logic to prevent duplicated events to exist in your project
- Added an integration event
- Added new default event and people properties

---

## [v2.0.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.0.0)
### September 24th - 2019

#### This version is a complete rewrite of the library to support ALL platforms that unity can compile to.

The basis for this rewrite was https://github.com/mixpanel/mixpanel-unity/issues/10 to support WebGL but since the library was rewriten in plain c# it should work for any platform unity can compile to.

The API has stayed compliant with the documentation though there maybe a few changes to a few of the mixpanel properties that come though automatically due to unity not having access to certain system/device information easily please reachout to us if there is something missing after you upgrade and we can introspect it

The github repo has also been structured so that it supports the Unity 2018.4 package manager (please see the README for package manager install instructions)

This version of the library should support backwards compatibility with Unity 2018.x but it has only been tested with the 2018 LTS release.

---

## [v1.1.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/v1.1.1)
### December 18th - 2017

Bug fixes

---

## [v1.1.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/v1.1.0)
### October 6th - 2017

Improvements
Persist alias and protect users from identifying the user as their alias
Reset distinct_id and alias when reset is called
Clean ups
Fixes
Switching platforms could lead to MixpanelPostProcessor been executed at the wrong time
Reversed the attribution of app build number and version string ($app_build_number and $app_version_string)
Fix crash occurring only for Android when AdvertisingIdClient.Info.getId() was returning null.

---

## [v1.0.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/1.0.1)
### June 16th - 2016

iOS
Added optional support for advertisingIdentifier
Added support for Bitcode
Windows
Added missing dependency for x86 and x86_64
All Platforms
Networking now respects the HTTP Retry-After header
Networking now backs off exponentially on failure

---

## [v1.0.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/1.0.0)
### June 3rd - 2016

We are thrilled to release the official Mixpanel Unity SDK. Some links to get started:

* [Official documentation](https://mixpanel.com/help/reference/unity)
* [Full API Reference](http://mixpanel.github.io/mixpanel-unity/api-reference/annotated.html)
* [Sample application](https://github.com/mixpanel/mixpanel-unity/tree/master/deployments/UnityMixpanel/Assets/Mixpanel/Sample)










