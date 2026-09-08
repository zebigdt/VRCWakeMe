# VRCWakeMe

Windows tray app that plays a loud alarm when someone grabs and pulls a handle on your VRChat avatar.

Someone in VR grips the handle and tugs → VRChat sends that PhysBone's parameters over OSC → this app loops an alarm on the headset (or any output device you pick) until you dismiss it or it hits the max duration.

A poke will not do it. It takes a grip and a pull, so a hand brushing past you while you sleep cannot set the alarm off.

This is **PC VRChat only**. OSC does not run on Quest.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
- VRChat with OSC enabled (Action Menu → OSC → Enable)
- A grab handle on the avatar you sleep in — see [Avatar setup](#avatar-setup)

## Avatar setup

[`unity/VRCWakeMe`](unity/VRCWakeMe) is a drag-in folder for your avatar project. It holds everything the avatar side needs:

| File | What it is |
| --- | --- |
| `VRCWakeMe Handle.prefab` | Grabbable **VRC PhysBone** handle (grab radius 0.12 m, parameter `WakeMe`, immobile so your own movement cannot swing it) |
| `Editor/VRCWakeMeSetup.cs` | Editor menu that drops the prefab on your chest and registers its parameters for OSC |

The project needs the VRChat Avatars SDK (3.5 or newer).

1. Drag the `VRCWakeMe` folder into your project's `Assets`, or import `VRCWakeMe-Avatar.unitypackage` from a release.
2. Select your avatar in the Hierarchy — the object with the **VRC Avatar Descriptor**.
3. **Tools → VRCWakeMe → Add wake handle to avatar**.
4. Move `VRCWakeMe Handle` to somewhere a hand can reach, adjust **Radius** on the PhysBone if you want a bigger or smaller grab target, and upload.

Step 3 does two things: it parents the handle to your chest bone, and it adds `WakeMe_IsGrabbed` (Bool) and `WakeMe_Stretch` (Float) to the avatar's Expression Parameters as unsynced, unsaved entries. Both are required. VRChat only sends a parameter over OSC if it is listed there, so the prefab on its own stays silent. If you drag the prefab in by hand instead of using the menu, run **Tools → VRCWakeMe → Register wake parameters on avatar** afterwards.

Rather build the handle yourself? [docs/avatar-setup.md](docs/avatar-setup.md) walks through it component by component and covers what to check in VRChat's overlays when nothing fires.

To produce the importable package for a release:

```powershell
powershell -ExecutionPolicy Bypass -File tools/build-unitypackage.ps1
```

That writes `dist/VRCWakeMe-Avatar.unitypackage`.

## Build and run

```bash
dotnet test VRCWakeMe.sln
dotnet run --project src/VRCWakeMe.App/VRCWakeMe.App.csproj
```

The app lives in the system tray. Settings open when it launches.

- **Activated** — grabs play the alarm. Turn this off when you are awake.
- **Dismiss alarm** — the large button under the switch, or the same item in the tray menu. It is greyed out unless an alarm is going off.
- **Bring to foreground on alarm** — when the alarm starts, the window pops in front of whatever you are doing and focuses the dismiss button, so you can stop it with one click or the spacebar. On by default; uncheck it to leave the window where it was.
- **Settings** — output device (pick your headset), volume, cooldown, duration, custom sound.
- Left-click the tray icon to open settings.

VRChat should show a HUD notice that it is sending OSC to **VRCWakeMe**. You should not need to type ports. Action Menu → **OSC → OSC Debug** shows four tiles — `armed`, `disarmed`, `grabbed`, `pulled` — and the ones matching the current state keep flashing.

## How it behaves

| Setting | Default |
| --- | --- |
| Cooldown | 20 seconds after a wake starts (holding the grab cannot spam) |
| Max duration | 45 seconds, then the alarm stops on its own |
| Trigger | Handle grabbed **and** pulled 15% through its stretch range — `WakeMe_IsGrabbed` true while `WakeMe_Stretch` ≥ 0.15, about a centimeter on the prefab's handle |

If an avatar exposes `WakeMe_IsGrabbed` but no `WakeMe_Stretch`, the bare grab wakes you instead, since there is no pull to measure.

Settings are stored in `%AppData%\VRCWakeMe\settings.json`.

## Later

A public website (sign-in, same-instance check, join link) is intentionally not in v1. See [DESIGN.md](DESIGN.md).
