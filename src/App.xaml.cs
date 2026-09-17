using System.Windows;
using System.Windows.Threading;

namespace VRCWakeMe;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private WakeCoordinator _wake = null!;
    private OscGrabTracker _grabs = null!;
    private AlarmPlayer _player = null!;
    private OscLink? _osc;
    private TrayIcon _tray = null!;
    private DispatcherTimer _timer = null!;
    private SettingsWindow? _settingsWindow;
    private string _status = "Not linked with VRChat";
    private long _lastDebugMs;
    private bool _testing;
    private CancellationTokenSource? _testCts;
    private DispatcherTimer? _saveTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, @"Local\VRCWakeMe.Tray", out var created);
        if (!created)
        {
            System.Windows.MessageBox.Show("VRCWakeMe is already running.", "VRCWakeMe");
            Shutdown();
            return;
        }

        base.OnStartup(e);
        AppTheme.Start();

        _store = new SettingsStore();
        _settings = _store.Load();
        _wake = new WakeCoordinator
        {
            Armed = _settings.Armed,
            Cooldown = TimeSpan.FromSeconds(_settings.CooldownSeconds),
            MaxDuration = TimeSpan.FromSeconds(_settings.MaxDurationSeconds)
        };
        _grabs = new OscGrabTracker();
        _player = new AlarmPlayer();
        _wake.AlarmStarted += () => Dispatcher.BeginInvoke(() =>
        {
            StopTestTimer();
            _testing = false;
            _player.Play(_settings, loop: true);
            if (_settings.ForegroundOnAlarm) BringToForeground();
        });
        _wake.AlarmStopped += () => Dispatcher.BeginInvoke(_player.Stop);
        _wake.StateChanged += () => Dispatcher.BeginInvoke(RefreshTray);

        try
        {
            _osc = OscLink.Start();
            // VRChat streams a lot of avatar parameters. Only wake-handle traffic
            // needs the UI thread; link status is watched by the 250 ms timer.
            _osc.MessageReceived += message =>
            {
                var address = message.Address;
                if (!address.Equals(OscAddresses.Grabbed, StringComparison.Ordinal) &&
                    !address.Equals(OscAddresses.Stretch, StringComparison.Ordinal) &&
                    !address.Equals(OscAddresses.AvatarChange, StringComparison.Ordinal))
                {
                    return;
                }

                Dispatcher.BeginInvoke(() => OnOscMessage(message));
            };
            _osc.LinkChanged += () => Dispatcher.BeginInvoke(() =>
            {
                // Once VRChat stops sending, whatever the handle was last doing no
                // longer holds, so it must not survive into the next link.
                if (_osc?.IsLinked != true) _grabs.Reset();
                RefreshConnectionStatus();
            });
        }
        catch (Exception)
        {
            _status = "Not linked with VRChat";
        }

        _tray = new TrayIcon();
        _tray.ArmedChanged += SetArmed;
        _tray.DismissRequested += DismissAlarm;
        _tray.OpenSettingsRequested += ShowSettings;
        _tray.ExitRequested += Shutdown;
        RefreshTray();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) =>
        {
            _wake.Tick();
            _osc?.Tick();
            RefreshConnectionStatus();
            PushOscDebug(force: false);
        };
        _timer.Start();

        StartupRegistration.ClearLegacyEntry();
        RefreshConnectionStatus();
        ShowSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        StopTestTimer();
        FlushSettings();
        _wake?.Dismiss();
        _player?.Dispose();
        _osc?.Dispose();
        _tray?.Dispose();
        _mutex?.Dispose();
        AppTheme.Stop();
        base.OnExit(e);
    }

    private void OnOscMessage(OscMessage message)
    {
        RefreshConnectionStatus();
        var grab = _grabs.Observe(message.Address, message.FirstArgument);
        if (grab.Changed) PushOscDebug(force: true);
        if (grab.Wake) _wake.RequestWake("osc");
    }

    private void RefreshConnectionStatus()
    {
        var text = _osc?.IsLinked == true ? "Linked with VRChat" : "Not linked with VRChat";
        if (text == _status) return;
        _status = text;
        _settingsWindow?.SetStatus(text);
    }

    private void PushOscDebug(bool force)
    {
        if (_osc is not { IsLinked: true }) return;
        if (!force && Environment.TickCount64 - _lastDebugMs < 1000) return;
        _lastDebugMs = Environment.TickCount64;
        _osc.SendDebug(_wake.Armed, _grabs.AnyGrabbed, _grabs.AnyPulled);
    }

    private bool AlarmAudible => _wake.IsPlaying || _testing;

    private void RefreshTray()
    {
        var alarming = AlarmAudible;
        _tray.SetState(_wake.Armed, alarming);
        _settingsWindow?.SetArmed(_wake.Armed);
        _settingsWindow?.SetAlarmPlaying(alarming);
    }

    private void BringToForeground()
    {
        ShowSettings();
        if (_settingsWindow is null) return;

        // The state change that enables the button is queued behind this call, and
        // WPF will not focus a disabled control, so sync it up front.
        _settingsWindow.SetAlarmPlaying(AlarmAudible);
        WindowForeground.Bring(_settingsWindow);
        _settingsWindow.FocusDismiss();
    }

    private void DismissAlarm()
    {
        var shouldDisarm = _settings.DisarmAfterDismiss && (_wake.IsPlaying || _testing);
        StopTestTimer();
        _testing = false;
        _wake.Dismiss();
        _player.Stop();
        if (shouldDisarm) SetArmed(false);
        else RefreshTray();
    }

    private void StopTestTimer()
    {
        _testCts?.Cancel();
        _testCts?.Dispose();
        _testCts = null;
    }

    private async void EndTestAfterAsync(int seconds, CancellationToken token)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        await Dispatcher.InvokeAsync(() =>
        {
            if (!_testing) return;
            _testing = false;
            if (!_wake.IsPlaying) _player.Stop();
            RefreshTray();
        });
    }

    private void SetArmed(bool armed)
    {
        _settings.Armed = armed;
        _wake.Armed = armed;
        SaveSettings();
        PushOscDebug(force: true);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            _settingsWindow = new SettingsWindow(
                _settings, _player.ListDevices(), _status, _wake.Armed, OnSettingsChanged, SetArmed);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "VRCWakeMe settings");
            return;
        }

        _settingsWindow.TestRequested += () =>
        {
            if (_wake.IsPlaying) return;
            StopTestTimer();
            _testing = true;
            try { _player.Play(_settings, loop: true); }
            catch (Exception ex)
            {
                _testing = false;
                System.Windows.MessageBox.Show($"Could not play alarm: {ex.Message}", "VRCWakeMe");
                RefreshTray();
                return;
            }

            RefreshTray();
            _testCts = new CancellationTokenSource();
            EndTestAfterAsync(_settings.MaxDurationSeconds, _testCts.Token);
        };
        _settingsWindow.DismissRequested += DismissAlarm;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.SetAlarmPlaying(AlarmAudible);
        try { _settingsWindow.Show(); }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString(), "VRCWakeMe settings"); }
    }

    private void OnSettingsChanged()
    {
        _wake.Cooldown = TimeSpan.FromSeconds(_settings.CooldownSeconds);
        _wake.MaxDuration = TimeSpan.FromSeconds(_settings.MaxDurationSeconds);
        SaveSettings();
    }

    private void SaveSettings()
    {
        // Spinners and the volume slider fire Persist many times a second. Writing
        // the file on the UI thread there makes every click feel stuck.
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer?.Stop();
        WriteSettingsAsync();
    }

    private void FlushSettings()
    {
        _saveTimer?.Stop();
        try { _store.Save(_settings); }
        catch (Exception)
        {
        }
    }

    private void WriteSettingsAsync()
    {
        var snapshot = _settings.Clone();
        var store = _store;
        _ = Task.Run(() =>
        {
            try { store.Save(snapshot); }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                    System.Windows.MessageBox.Show($"Could not save settings: {ex.Message}", "VRCWakeMe"));
            }
        });
    }
}
