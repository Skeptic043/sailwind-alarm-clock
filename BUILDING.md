# Building Alarm Clock

## Requirements

- Windows with PowerShell 7 and Windows PowerShell 5.1.
- .NET SDK 10 (the checks target .NET 10) and .NET Framework 4.7.1 reference assemblies, available through the developer pack or compatible SDK tooling.
- A local Sailwind installation and BepInExPack 5.4.2305, including BepInEx's `Mono.Cecil.dll`, `MonoMod.Utils.dll` and `MonoMod.RuntimeDetour.dll` for the binding checks.

Game and loader assemblies are build references only. They are not included in this repository or its packages.

## Build

From the project directory, run:

```powershell
pwsh -File ./Build.ps1 -GameManagedDir 'C:\Games\Sailwind\Sailwind_Data\Managed' -ReferenceDir 'C:\Games\Sailwind\BepInEx\core'
```

Use the paths for your own installation or mod profile. Alternatively, place `BepInEx.dll` and `0Harmony.dll` in `.local/references/` and omit `-ReferenceDir`. Without either, the build uses the Sailwind Mod Synchronizer default pack's `BepInEx/core` folder.

The Release output is `bin/Release/net471/AlarmClock.dll`.

## Test

From the project directory:

```powershell
pwsh -File ./Test.ps1 -GameManagedDir 'C:\Games\Sailwind\Sailwind_Data\Managed' -ReferenceDir 'C:\Games\Sailwind\BepInEx\core'
```

Each suite runs in a separate process and any failure makes the command fail.

### Coverage

| Suite | Behavior protected |
| --- | --- |
| `logic` | Alarms fire once when their time is crossed, including large sleep frames and midnight rollover, never fire again after a reload, skip when more than an hour late, step and wrap the time of day correctly, and count every scroll-wheel notch once. The tone setting moves every sound by the same amount around its natural pitch, and the WAV reader accepts the shipped sounds and refuses unsupported files. Saved alarm data round-trips, and unreadable or unknown data is left unwritten. |
| production build | The plugin compiles against the installed game and loader assemblies. |
| `tests/binding` | Every Harmony patch generates a replacement against the installed game, and every game member the plugin reads still exists, including the hover prompt's controls text, tooltip line and R icon. No game code is executed. |

These checks do not cover how the alarm sounds in game, the seamless switch from loop to tail, waking, controls, look text or save and load in the running game. Those need an in-game test.

## Package

Run `Package.ps1` under PowerShell 7 with the same reference arguments. It builds the DLL, checks the manifest and release notes, and writes a plugin ZIP under `artifacts/`. Add `-SkipBuild` to package an already built Release DLL. The package includes `icon.png` when the file exists.
