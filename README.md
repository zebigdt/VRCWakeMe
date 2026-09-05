# VRCWakeMe

Windows tray app that plays a loud alarm when someone grabs a handle on your VRChat avatar.

Someone in VR grabs the invisible PhysBone handle → VRChat sends `grabbed_IsGrabbed` over OSC → this app loops an alarm on the headset (or any output device you pick) until you dismiss it or it hits the max duration.

This is **PC VRChat only**. OSC does not run on Quest.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
- VRChat with OSC enabled (Action Menu → OSC → Enable)
- Avatar grab handle: [docs/avatar-setup.md](docs/avatar-setup.md)

## Build and run

```bash
dotnet test VRCWakeMe.sln
dotnet run --project src/VRCWakeMe.App/VRCWakeMe.App.csproj
```

The app lives in the system tray. Settings open when it launches.

- **Activated** — grabs play the alarm. Turn this off when you are awake.
- **Dismiss alarm** — stop the current sound.
- **Settings** — output device (pick your headset), volume, cooldown, duration, custom sound, start with Windows.
- Left-click the tray icon to open settings.

VRChat should show a HUD notice that it is sending OSC to **VRCWakeMe**. You should not need to type ports.

## How it behaves

| Setting | Default |
| --- | --- |
| Cooldown | 20 seconds after a wake starts (holding the grab cannot spam) |
| Max duration | 45 seconds, then the alarm stops on its own |
| Trigger | Rising edge of `/avatar/parameters/grabbed_IsGrabbed` (`false` → `true`) |

Settings are stored in `%AppData%\VRCWakeMe\settings.json`.

## Later

A public website (sign-in, same-instance check, join link) is intentionally not in v1. See [DESIGN.md](DESIGN.md).
