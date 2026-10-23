# Alarm Clock

Alarm Clock lets you set a daily alarm on any chronometer. When the time comes, the chronometer rings its bell and wakes you if you're asleep nearby.

## Install

### Mod managers

Install Alarm Clock through [Sailwind Mod Synchronizer](https://github.com/foxyv/SailwindModSynchronizer/releases/latest), r2modman or Thunderstore Mod Manager, then launch Sailwind through your manager. Dependencies are installed automatically.

### Manual installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) in your Sailwind game folder, following its installation instructions.
2. Download and extract the [Alarm Clock ZIP](https://github.com/Skeptic043/sailwind-alarm-clock/releases/latest). Copy its `BepInEx/plugins/AlarmClock` folder into `BepInEx/plugins` in your Sailwind folder.
3. Launch Sailwind normally.

## How it works

Hold a chronometer you own, or look at one hanging on a wall, and hold `Z`. Scroll to set the alarm time in 15-minute steps, and press R (or right-click) to switch the alarm on or off. Setting a time switches the alarm on. Looking at the chronometer shows its alarm time, and holding `Z` while looking at it shows how to set it. Alarms follow the time on the chronometer's face and repeat every day. Each chronometer keeps its own alarm.

When the alarm goes off, the chronometer rings and wakes you from sleep. Look at it or hold it to see "stop alarm" beside the R icon, and press R to stop it. Otherwise it stops by itself after a few seconds. A chronometer only rings when you're close to it, so keep it in your inventory or near your bed. Alarms stay silent while you rest at an inn.

## Configuration

Settings are in `BepInEx/config/com.skeptic043.sailwind.alarmclock.cfg` after the first launch, or in game with the BepInEx 5 version of [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager). Changes apply immediately.

| Setting | Default | Options |
| --- | --- | --- |
| `Alarm modifier` | `Z` | The key to hold while scrolling or pressing R to set the alarm. |
| `Ring range` | `25` | How close, in meters, you need to be for a chronometer to ring and wake you, from 5 to 100. |
| `Ring duration` | `5` | How many seconds an alarm rings before it stops by itself, from 1 to 60. A recorded clock then winds down for a few seconds. |
| `Volume` | `0.3` | Alarm bell volume, from 0 to 1. |
| `Alarm sound` | `WarmClock` | `WarmClock` (warm mechanical clock), `BrightClock` (bright twin-bell clock) or `OriginalBell` (the original Sailwind bell). A change applies from the next alarm. |
| `Bell tone` | `50` | From 0 (deeper and heavier) to 100 (brighter and smaller). 50 is each sound's natural tone. |
| `Show controls tooltip` | `true` | Show how to set the alarm while you hold `Z` and look at a chronometer. The alarm time is always shown. |

## Compatibility

No known incompatibilities. While `Z` is held on a chronometer, scrolling sets the alarm instead of moving the chronometer with [Deft Hands](https://github.com/KeeviDev/SailwindDeftHands).

## AI Use

AI was used to write all of the code in this project. The original concept, design direction, testing, debugging, and release decisions are my own. If you prefer not to use mods developed with AI assistance, I understand and respect that choice.

## Issues and links

[Report an issue](https://github.com/Skeptic043/sailwind-alarm-clock/issues) with your mod version and `BepInEx/LogOutput.log`.

[Source code](https://github.com/Skeptic043/sailwind-alarm-clock) · [Release notes](CHANGELOG.md) · [MIT License](LICENSE) · [Support on Ko-fi](https://ko-fi.com/skeptic043) · skeptic043
