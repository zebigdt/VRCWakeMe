# Development

The app is a single project: every source file sits directly under [`src`](../src), with the tests directly under [`tests`](../tests). [code-map.md](code-map.md) lists every file and the classes and functions inside it.

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build, not only the runtime.

## Run a debug build

[`scripts/dev-test-run.bat`](../scripts/dev-test-run.bat) is local testing only and is not part of a release. It runs the tests, then starts a debug build if they pass, and stops without launching if they fail.

```bash
scripts\dev-test-run.bat
```

The same two commands by hand:

```bash
dotnet test VRCWakeMe.sln
dotnet run --project src/VRCWakeMe.csproj
```

The avatar folder in the repo is [`unity/VRCWakeMe`](../unity/VRCWakeMe). Drag that into a Unity project's `Assets` if you are iterating on the prefab instead of importing a packaged release. [`unity/VRCWakeMe.Dev`](../unity/VRCWakeMe.Dev) is development only and is never shipped.

## Cut a release

[`scripts/build-release.ps1`](../scripts/build-release.ps1) publishes the app as a single exe, packs the avatar assets into a `.unitypackage`, and zips both together. Both scripts work out the repo root themselves, so they can be run from any directory.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1
```

That writes two files into `dist/`:

- `VRCWakeMe-<version>-win-x64.zip` — `VRCWakeMe.exe`, its `Assets` folder, and the `.unitypackage`. This is the download to attach to a GitHub release.
- `VRCWakeMe-Avatar.unitypackage` — the avatar half on its own, for anyone who already has the app.

The exe is framework-dependent, which is what keeps it under a megabyte, so it needs the .NET 8 Desktop Runtime on the machine that runs it. The version in the zip name comes from `<Version>` in [`src/VRCWakeMe.csproj`](../src/VRCWakeMe.csproj), so bump it there before cutting a release. `-Configuration`, `-Runtime`, `-AssetRoot` and `-OutputDirectory` are available if you need to override the defaults.
