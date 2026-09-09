# VRCWakeMe

A Windows tray app that plays a loud alarm when someone grabs and pulls a handle on your VRChat avatar.

A poke will not do it. It takes a grip and a tug, so a hand brushing past you while you sleep cannot set the alarm off.

This is **PC VRChat only**. OSC does not run on Quest.

## Requirements

- Windows 10 or 11
- The [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (the desktop pack, not just the console runtime)
- VRChat with OSC enabled (Action Menu → OSC → Enable)
- The wake handle on the avatar you sleep in — see [Avatar setup](#avatar-setup)

## Install

1. Download the latest `VRCWakeMe-*-win-x64.zip` from [Releases](https://github.com/zebigdt/VRCWakeMe/releases).
2. Unzip it somewhere you will leave it, then run `VRCWakeMe.exe`.
3. In VRChat, open the Action Menu → **OSC → Enable**.

The app lives in the system tray. Settings open when it launches. VRChat should show a HUD notice that it is sending OSC to **VRCWakeMe** — you do not need to type ports.

The zip also includes `VRCWakeMe-Avatar.unitypackage`. Keep it for the next section.

## Avatar setup

Your Unity project needs the VRChat Avatars SDK (3.5 or newer).

1. In Unity, import `VRCWakeMe-Avatar.unitypackage` from the zip (**Assets → Import Package → Custom Package**).
2. Select your avatar in the Hierarchy — the object with the **VRC Avatar Descriptor**.
3. **Tools → VRCWakeMe → Add wake handle to avatar**.
4. Move `VRCWakeMe Handle` to somewhere a hand can reach, then upload.

The menu parents the handle to your chest and registers the parameters VRChat needs to send over OSC. If you drag the prefab onto the avatar yourself instead, run **Tools → VRCWakeMe → Register wake parameters on avatar** afterwards.

Want to build the handle by hand, or the overlay is empty after upload? [docs/avatar-setup.md](docs/avatar-setup.md) walks through the components and what to check in VRChat.

## Using the app

- **Activated** — grabs play the alarm. Turn this off when you are awake.
- **Dismiss alarm** — the large button under the switch, or the same item in the tray menu. It is greyed out unless an alarm is going off.
- **Bring to foreground on alarm** — when the alarm starts, the window pops in front of whatever you are doing and focuses the dismiss button, so you can stop it with one click or the spacebar. On by default.
- **Settings** — output device (pick your headset), volume, cooldown, duration, custom sound.
- Left-click the tray icon to open settings.

Action Menu → **OSC → OSC Debug** shows four tiles — `armed`, `disarmed`, `grabbed`, `pulled`. The ones matching the current state keep flashing.

## How it behaves

| Setting | Default |
| --- | --- |
| Cooldown | 20 seconds after a wake starts, so holding the grab cannot spam |
| Max duration | 45 seconds, then the alarm stops on its own |
| Trigger | The handle must be grabbed **and** pulled a little (about a centimeter on the shipped handle) |

Settings are stored in `%AppData%\VRCWakeMe\settings.json`.

## For developers

Building from source, the release scripts, and the code layout are in [docs/development.md](docs/development.md). Why it is designed this way is in [docs/design.md](docs/design.md).
