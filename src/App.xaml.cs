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
            _player.Play(_settings, loop: true);
            if (_settings.ForegroundOnAlarm) BringToForeground();
        });
        _wake.AlarmStopped += () => Dispatcher.BeginInvoke(_player.Stop);
        _wake.StateChanged += () => Dispatcher.BeginInvoke(RefreshTray);

        try
        {
            _osc = OscLink.Start();
            _osc.MessageReceived += message => Dispatcher.BeginInvoke(() => OnOscMessage(message));
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
        _tray.DismissRequested += () => _wake.Dismiss();
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

    private void RefreshTray()
    {
        _tray.SetState(_wake.Armed, _wake.IsPlaying);
        _settingsWindow?.SetArmed(_wake.Armed);
        _settingsWindow?.SetAlarmPlaying(_wake.IsPlaying);
    }

    private void BringToForeground()
    {
        ShowSettings();
        if (_settingsWindow is null) return;

        // The state change that enables the button is queued behind this call, and
        // WPF will not focus a disabled control, so sync it up front.
        _settingsWindow.SetAlarmPlaying(_wake.IsPlaying);
        WindowForeground.Bring(_settingsWindow);
        _settingsWindow.FocusDismiss();
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

        _settingsWindow.TestRequested += async () =>
        {
            try { await _player.PlayPreviewAsync(_settings, TimeSpan.FromSeconds(3)); }
            catch (Exception ex) { System.Windows.MessageBox.Show($"Could not play alarm: {ex.Message}", "VRCWakeMe"); }
            finally { if (!_wake.IsPlaying) _player.Stop(); }
        };
        _settingsWindow.DismissRequested += () => _wake.Dismiss();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.SetAlarmPlaying(_wake.IsPlaying);
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
        try { _store.Save(_settings); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not save settings: {ex.Message}", "VRCWakeMe"); }
    }
}
