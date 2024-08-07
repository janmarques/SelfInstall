## SelfInstall 
Make Windows Services install themselves

NuGet:
https://www.nuget.org/packages/SelfInstall
Github:
https://github.com/janmarques/SelfInstall

## Quickstart
Install the package.

In Program.cs, add the following snippet:


```
using SelfInstall;
using static SelfInstall.Extensions;

var builder = WebApplication.CreateBuilder(args);

[...]

builder.ConfigureSelfInstall(new SelfInstallOptions
{
    [...]
});

var app = builder.Build();

var didInstallationProcess = await app.RunSelfInstall(args);
if (didInstallationProcess)
{
    return;
}

[...]

app.Run("http://localhost:1991");

```
When the program runs in interactive mode (ie in a console), it will get installed as a Windows Service and quit.
When it runs in non-interactive mode (ie as a Windows Service), it will run normally.

Installing a Windows Service requires admin privileges, so make sure to add this in an app.manifest file:
```
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1" >
	<assemblyIdentity version="1.0.0.0" name="MyApplication.app" />
	<trustInfo xmlns="urn:schemas-microsoft-com:asm.v2" >
		<security >
			<requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3" >
				<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
			</requestedPrivileges >
		</security >
	</trustInfo >
</assembly >
```
And reference the manifest in your csproj
```
	<TargetFramework>net8.0-windows</TargetFramework>
	<ApplicationManifest>app.manifest</ApplicationManifest>
```

Windows Services are (obviously) only available on Windows, hence the package targets -windows.

A full example is available in `SelfInstall.WebSample`.


## Tips / known annoyances
* Installing/removing services can be tricky and behave strange when observed.
Eg when you get an error: `The specified service has been marked for deletion.` => close services.msc, task manager and Visual Studio.

* There is not a lot of logging. Check the Event Viewer.

* When this package is added and you run it in Visual Studio, it will start installing it. 
If you want to keep debugging your existing code from the service itself, supply the argument "local", and the installation will be skipped and the program will run as if it were installed
You can do this via the `launchSettings.json` by adding `"commandLineArgs": "local"`

* When you configure SelfInstall to copy the exe to a different folder and run it from there, make sure the exe is self-contained (because it is the only file that gets copied, so it won't be able to reach any needed dll's).
Use `<PublishSingleFile>true</PublishSingleFile>` in your `FolderProfile.pubxml`