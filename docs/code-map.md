# Code map

Every file in the repo and what lives inside it. The app is one project, `src/VRCWakeMe.csproj`, with the tests in `tests/VRCWakeMe.Tests.csproj`. Everything shares the `VRCWakeMe` namespace.

## How a wake happens

VRChat finds the app over mDNS and starts sending avatar parameters to UDP 9001 (`OscLink`) → each message is parsed (`OscPacketParser`) and fed to the grab tracker (`OscGrabTracker`) → a grab that is also pulled asks for a wake (`WakeCoordinator`) → the alarm loops (`AlarmPlayer`) and the window comes forward (`App`, `WindowForeground`) until it is dismissed.

## src

### `VRCWakeMe.csproj`

The whole app: WPF plus Windows Forms (for the tray icon), targeting `net8.0-windows`, with NAudio for playback and MeaMod.DNS for the mDNS advertisement. Copies `Assets/alarm.wav` and the icons next to the binary.

### `App.xaml` / `App.xaml.cs`

`App` — the application entry point and the wiring between every other piece. Holds the single-instance mutex, the settings store, the wake coordinator, the grab tracker, the alarm player, the OSC link, the tray icon, and a 250 ms timer that ticks the alarm timeout, the link timeout, and the debug output.

| Member | Does |
| --- | --- |
| `OnStartup` | Builds every part, wires the events, clears the legacy startup registry entry, shows the settings window |
| `OnExit` | Stops the timer, dismisses the alarm, disposes the player, link, tray and mutex |
| `OnOscMessage` | Feeds a parameter into the tracker, pushes debug on a state change, requests a wake on a pull |
| `RefreshConnectionStatus` | Updates the "Linked with VRChat" line, only when the text actually changed |
| `PushOscDebug` | Sends the four debug tiles, rate limited to once a second unless forced |
| `RefreshTray` | Pushes armed and audible-alarm state into the tray icon and the window |
| `BringToForeground` | Shows the window, enables the dismiss button, raises the window, focuses dismiss |
| `DismissAlarm` | Stops a real or test alarm, and turns the switch Inactive when that setting is on |
| `SetArmed` | Arms or disarms, then saves |
| `ShowSettings` | Creates the settings window on demand, or re-shows the existing one |
| `OnSettingsChanged`, `SaveSettings` | Applies edited settings to the live objects and writes them to disk |

### `SettingsWindow.xaml` / `SettingsWindow.xaml.cs`

`SettingsWindow` — the only window. Active/Inactive switch, large dismiss button, output device, volume, cooldown, duration, custom sound, the foreground and disarm-after-dismiss checkboxes, and the test button.

| Member | Does |
| --- | --- |
| `SetStatus`, `SetArmed`, `SetAlarmPlaying`, `FocusDismiss` | The surface `App` uses to push state in and focus dismiss |
| `TestRequested`, `DismissRequested` | Raised by the test and dismiss buttons |
| `LoadFromSettings`, `Persist` | Move values between the controls and `AppSettings` |
| `OnArmedToggle`, `UpdateArmedLabel` | Keep the switch and its label in step |
| `RefreshSoundUi`, `BrowseSound` | Show which sound is in use and pick a custom one |

### `Osc.cs`

The OSC protocol and the trigger rule. No UI, no sockets — this is the part the tests drive.

| Type | Does |
| --- | --- |
| `OscMessage` | An address plus its arguments, with `FirstArgument` for the common case |
| `OscGrab` | The result of observing one parameter: handle name, grabbed, pulled, whether it was a handle at all, whether state changed, and whether it should wake you |
| `OscAddresses` | The address constants: the `/avatar/parameters/` prefix, the `_IsGrabbed` and `_Stretch` suffixes, the `WakeMe` handle, `/avatar/change`, and the four `/VRCWakeMe/...` debug tiles |
| `OscValue` | `IsOn` and `AsFloat`, which turn whatever VRChat sent (bool, int, float, string) into a usable value |
| `OscGrabTracker` | Tracks only `WakeMe_IsGrabbed` and `WakeMe_Stretch`. `Observe` returns the rising edge of "grabbed and pulled past `PullThreshold`", `AnyGrabbed` and `AnyPulled` drive the debug tiles, `Reset` forgets everything when the link drops or the avatar changes |
| `OscWriter` | Builds outgoing packets: `Write` plus the padded string and big-endian number helpers |
| `OscPacketParser` | `Parse` for incoming packets, handling bundles, and the argument readers for the tags VRChat uses |

### `OscLink.cs`

`OscLink` — everything network. Advertises the app over mDNS, answers VRChat's OSCQuery probes over HTTP, and listens for parameters over UDP.

| Member | Does |
| --- | --- |
| `Start` | Binds the UDP and HTTP ports, publishes the mDNS service, starts both loops |
| `IsLinked` | True while VRChat has been heard from recently, so turning OSC off in game shows up |
| `MessageReceived`, `LinkChanged` | Events the app subscribes to |
| `Tick` | Expires the link after silence and re-announces the service periodically |
| `SendDebug` | Sends the armed, disarmed, grabbed and pulled tiles, priming all four once so they appear in a fixed order |
| `ReceiveLoopAsync`, `HttpLoopAsync`, `HandleHttp` | The UDP receive loop and the OSCQuery handshake responses |
| `Listen`, `TryListen`, `TryAnnounce`, `GuessLanIp` | Port binding, mDNS announcing, and picking the LAN address to advertise |

### `Wake.cs`

The alarm state machine, independent of sound and UI.

| Type | Does |
| --- | --- |
| `WakeResult` | `Started`, `AlreadyPlaying`, `Disarmed`, `OnCooldown` |
| `IWakeTrigger` | The one hook a wake source needs, so a future web trigger can reuse it |
| `WakeCoordinator` | `Armed`, `IsPlaying`, `Cooldown`, `MaxDuration`, the `AlarmStarted` / `AlarmStopped` / `StateChanged` events, `RequestWake` for the arming, cooldown and duplicate rules, `Dismiss`, and `Tick` for the max-duration cutoff |

### `Settings.cs`

| Type | Does |
| --- | --- |
| `AppSettings` | Armed, cooldown, duration, volume, output device, custom sound, foreground on alarm, disarm after dismiss, plus `Clamp` to keep loaded values sane |
| `SettingsStore` | `Load` and `Save` against `%AppData%\VRCWakeMe\settings.json`, falling back to defaults when the file is missing or unreadable |

### `Audio.cs`

| Type | Does |
| --- | --- |
| `AudioDeviceOption` | An output device as shown in the dropdown |
| `LoopStream` | Wraps a `WaveStream` and restarts it at the end so the alarm keeps going |
| `AlarmPlayer` | `ListDevices`, `Play` (looping, on the chosen device, at the chosen volume), `Stop`, and `BundledAlarmPath` for the shipped `alarm.wav` |

### `Tray.cs`

| Type | Does |
| --- | --- |
| `SleepIcons` | Draws the tray and window icons at runtime, so no image files are needed for them |
| `TrayIcon` | The tray icon and its menu, with `SetState` for armed and playing, and the `OpenSettingsRequested`, `ExitRequested`, `ArmedChanged` and `DismissRequested` events |

### `Ui.cs`

Small Windows-specific helpers.

| Type | Does |
| --- | --- |
| `StartupRegistration` | `ClearLegacyEntry` removes the old "Start with Windows" registry value from earlier versions |
| `WindowForeground` | `Bring` raises and focuses a window from the tray, working around Windows' foreground restrictions |
| `NumericTextBox` | `Attach` and `Read` keep the cooldown and duration boxes digits-only and inside their range |
| `AppTheme` | Follows the Windows light/dark setting and applies it to the window and title bar |

### `AssemblyInfo.cs`

The WPF `ThemeInfo` attribute. Nothing else.

### `Themes/Light.xaml`, `Themes/Dark.xaml`

Colour brushes for each theme, including the danger brushes the dismiss button uses. `App.xaml` holds the shared control styles.

### `Assets/`

`alarm.wav` is the bundled alarm. The `.ico` files are the app and tray icons.

## tests

### `WakeBehaviorTests.cs`

| Class | Covers |
| --- | --- |
| `WakeCoordinatorTests` | Arming, first wake, duplicate wakes while playing, cooldown before and after it expires, the max-duration cutoff, disarming mid-alarm, and the shared trigger hook |
| `OscGrabTrackerTests` | Grab with no stretch parameter, grab without a pull, pull while grabbed, needing a fresh pull after a re-grab, stretch with no grab, pokes and other PhysBones being ignored, avatar change clearing the handle, and the `OscValue` conversions |
| `OscPacketParserTests` | Bools, ints, floats, bundles, and a writer round trip |
| `SettingsStoreTests` | Settings surviving a save and load |

## Other folders

| Path | What it is |
| --- | --- |
| `unity/VRCWakeMe/` | The avatar side that ships to users: the PhysBone handle prefab and the editor menu that registers its parameters. See [avatar-setup.md](avatar-setup.md) |
| `unity/VRCWakeMe.Dev/` | Development only, never shipped: `WakeHandleGrabSim.cs` fakes a grab and pull in play mode so the chain can be tested without VR |
| `scripts/` | Local test runner and the release packer. See [development.md](development.md) |
