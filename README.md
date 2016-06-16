Latest Version 
--------------
##### _June 16th, 2016_ - [v1.0.1](https://github.com/mixpanel/mixpanel-unity/releases/tag/v1.0.1)

[See the changes](https://github.com/mixpanel/mixpanel-unity/releases/tag/v1.0.1).

Getting Started
---------------
Check out our [official documentation](https://mixpanel.com/help/reference/unity) to learn how to install the library in Unity. You will also learn how to make use of all the features we currently support!

Other links:
* Full API Reference: http://mixpanel.github.io/mixpanel-unity/api-reference/annotated.html
* Sample application: https://github.com/mixpanel/mixpanel-unity/tree/master/deployments/UnityMixpanel/Assets/Mixpanel/Sample

Want to Contribute?
-------------------
The Mixpanel library for Unity is an open source project, and we'd love to see your contributions!
We'd also love for you to come and work with us! Check out http://boards.greenhouse.io/mixpanel/jobs/25078#.U_4BBEhORKU for details.

Changelog
---------
See [wiki page](https://github.com/mixpanel/mixpanel-unity/wiki/Changelog).


Building the SDK
----------------

Add all dependencies of the Mixpanel SDK to your app using the following code in your terminal:

```
sh ./native/build/install_dependencies.sh
```

Then, run the following build script in your terminal: 

```
cd build
python build_all.py # on OS X
build_all.py # on Windows
```

Note that Unity must be closed while running build_all.py.

The build script will create all the project files (make, xcode, visual studio), perform the builds and run the tests suites. The test suites are run on: OS X, Windows, iOS Simulator and Android Device (if one is attached).
