# Building Alarm Clock

## Requirements

- Windows with PowerShell 7 and a .NET SDK capable of building the `net471` project.
- .NET Framework 4.7.1 reference assemblies, available through the developer pack or compatible SDK tooling.
- A local Sailwind installation and BepInExPack 5.4.2305.

Game and loader assemblies are build references only. They are not included in this repository or its packages.

## Build

From the project directory, run:

```powershell
pwsh -File ./Build.ps1 -GameManagedDir 'C:\Games\Sailwind\Sailwind_Data\Managed' -ReferenceDir 'C:\Games\Sailwind\BepInEx\core'
```

Use the paths for your own installation or mod profile. Alternatively, place `BepInEx.dll` and `0Harmony.dll` in `.local/references/` and omit `-ReferenceDir`. Without either, the build uses the Sailwind Mod Synchronizer default pack's `BepInEx/core` folder.

The Release output is `bin/Release/net471/AlarmClock.dll`.

Run the automated checks using [TESTING.md](TESTING.md).

## Local packages

Run `Package.ps1` under PowerShell 7 with the same reference arguments. It builds the DLL, checks the manifest and release notes, and writes a plugin ZIP under `artifacts/`. Add `-SkipBuild` to package an already built Release DLL. The package includes `icon.png` when the file exists.
