Latest Version 
--------------
##### _February 10th, 2020_ - [v2.1.2](https://github.com/mixpanel/mixpanel-unity/releases/tag/v2.1.3)

Supported Unity Version >= 2018.3. For older versions, you need to have `.NET 4.x Equivalent` selected as the scripting runtime version in your editor settings.

Getting Started
---------------
Check out our [official documentation](https://mixpanel.com/help/reference/unity) to learn how to make use of all the features we currently support!

Installation
------------

This library can be installed using the unity package manager system (Unity >= 2018.3) with git

1. In your unity project root open `./Packages/manifest.json`
2. Add the following line to the dependencies section:
```
"com.mixpanel.unity": "https://github.com/mixpanel/mixpanel-unity.git#v2.1.2"
```
3. Open Unity and the package should download automatically

Alternatively you can go to the [releases page](https://github.com/mixpanel/mixpanel-unity/releases) and download the .unitypackage file and have unity install that.

Examples
--------
Checkout our Examples by importing the `Examples.unitypackage` file located inside the `Mixpanel` folder after you follow the installation instructions above

Want to Contribute?
-------------------
The Mixpanel library for Unity is an open source project, and we'd love to see your contributions!
We'd also love for you to come and work with us! Check out http://boards.greenhouse.io/mixpanel/jobs/25078#.U_4BBEhORKU for details.

The best way to work on the Mixpanel library is the clone this repository and use a unity "local" package reference by creating a new unity project and opening the `./Packages/manifest.json` file and adding the following line under the `dependencies` section

```json
"com.mixpanel.unity": "file:C:/Path/to/cloned/repo/mixpanel-unity",
```

Changelog
---------
See [changelog](https://github.com/mixpanel/mixpanel-unity/tree/master/CHANGELOG.md) for details.
