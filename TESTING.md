# Testing Alarm Clock

## Requirements

- Windows, PowerShell 7 and Windows PowerShell 5.1.
- .NET SDK 10 and .NET Framework 4.7.1 reference assemblies.
- Independently supplied Sailwind and BepInEx 5 assemblies, as described in [BUILD.md](BUILD.md), including BepInEx's `Mono.Cecil.dll`, `MonoMod.Utils.dll` and `MonoMod.RuntimeDetour.dll`.

## Run

From the project directory:

```powershell
pwsh -File ./Test.ps1 -GameManagedDir 'C:\Games\Sailwind\Sailwind_Data\Managed' -ReferenceDir 'C:\Games\Sailwind\BepInEx\core'
```

Each suite runs in a separate process and any failure makes the command fail.

## Coverage

| Suite | Behavior protected |
| --- | --- |
| `logic` | Alarms fire once when their time is crossed, including large sleep frames and midnight rollover, never fire again after a reload, skip when more than an hour late, step and wrap the time of day correctly, and count every scroll-wheel notch once. Saved alarm data round-trips, and unreadable or unknown data is left unwritten. |
| production build | The plugin compiles against the installed game and loader assemblies. |
| `tests/binding` | Every Harmony patch generates a replacement against the installed game, and every game member the plugin reads still exists, including the hover prompt's controls text, tooltip line and R icon. No game code is executed. |

These checks do not cover how the bell sounds in game, waking, controls, look text or save and load in the running game. Those need an in-game test.
