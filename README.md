Consens SharpView
=================


Description
-----------
This utility provides an alternative to Consens WebView that runs locally and
doesn't require Java. In addition, it adds the option to (securely) store the
user name and password, which enabled auto-logon.

Build
-----
To avoid legal issues, none of the required libraries are included. So in order
to build the program, first create a folder named `lib` at the same location
where the project file resides.
Then place all [IKVM](http://www.ikvm.net/download.html) DLLs into that folder,
i.e. all `IKVM.*.dll` files.
Now compile the ULC Java Applet files to *.NET*. This is done by first copying
`ulc-base-client.jar`, `ulc-applet-client.jar`, `ulc-servlet-client.jar` and
`consens-ulc-extension.jar` from your Consens installation into a folder on the
development machine. Open a command prompt within that folder, execute

    ikvmc.exe -out:Consens.dll -target:library ulc-base-client.jar ulc-applet-client.jar ulc-servlet-client.jar consens-ulc-extension.jar

and place the generated `Consens.dll` into the project's `lib` folder.
Finally, build the project with Visual Studio or msbuild just as usual.

Usage
-----
The program takes three parameters: The first one is the base URL of ZcWebView,
which simply is `http://<server>:<port>/`. The second parameter allows to set
or override the user name, the third to additionally provide a password, which
is not recommended. Usually, only the first parameter should be supplied.

If a user has stored his or her password and wants to logon with a different
name, the program can be started while holding the `SHIFT` key, which brings
up the logon dialog again.
