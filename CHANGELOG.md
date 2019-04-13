# [v2.0.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.0.0)
### April 12th - 2018

#### This version is a complete rewrite of the library to support ALL platforms that unity can compile to.

The basis for this rewrite was https://github.com/mixpanel/mixpanel-unity/issues/10 to support WebGL but since the library was rewriten in plain c# it should work for any platform unity can compile to.

The API has stayed complient with the documentation though there maybe a few changes to a few of the mixpanel properties that come though automatically due to unity not having access to certain system/device information easily

The github repo has also been structured so that it supports the Unity 2018.3 package manager (please see the README for package manager install instructions)

---

# [v1.1.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/v1.1.1)
### December 18th - 2017

Bug fixes

---

# [v1.1.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/v1.1.0)
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

# [v1.0.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/1.0.1)
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

# [v1.0.0](https://github.com/mixpanel/mixpanel-unity/releases/tag/1.0.0)
### June 3rd - 2016

We are thrilled to release the official Mixpanel Unity SDK. Some links to get started:

* [Official documentation](https://mixpanel.com/help/reference/unity)
* [Full API Reference](http://mixpanel.github.io/mixpanel-unity/api-reference/annotated.html)
* [Sample application](https://github.com/mixpanel/mixpanel-unity/tree/master/deployments/UnityMixpanel/Assets/Mixpanel/Sample)
