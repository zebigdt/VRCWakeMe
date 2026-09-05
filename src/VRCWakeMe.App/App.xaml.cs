using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VRCWakeMe.App.Audio;
using VRCWakeMe.App.Osc;
using VRCWakeMe.Core;

namespace VRCWakeMe.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private WakeCoordinator _wake = null!;
    private OscTouchedTracker _touched = null!;
    private AlarmPlayer _player = null!;
    private UdpOscReceiver _osc = null!;
    private OscQueryHost _query = null!;
    private TrayIcon _tray = null!;
    private DispatcherTimer _timer = null!;
    private SettingsWindow? _settingsWindow;
    private string _status = "Not linked with VRChat";
    private bool _oscReady;
    private bool _receivedOsc;
    private int _connectionTick;

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
        _touched = new OscTouchedTracker();
        _player = new AlarmPlayer();

        _wake.AlarmStarted += () => Dispatcher.BeginInvoke(() => _player.Play(_settings, loop: true));
        _wake.AlarmStopped += () => Dispatcher.BeginInvoke(_player.Stop);
        _wake.StateChanged += () => Dispatcher.BeginInvoke(RefreshTray);

        try
        {
            _osc = new UdpOscReceiver(IPAddress.Loopback);
            _osc.MessageReceived += message => Dispatcher.BeginInvoke(() => OnOscMessage(message));
            _osc.Start();

            _query = new OscQueryHost();
            _query.Start(_osc.Port);
            _oscReady = true;
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
            _connectionTick++;
            if (_connectionTick % 8 == 0)
            {
                RefreshConnectionStatus();
            }
        };
        _timer.Start();

        StartupRegistration.Apply(_settings.StartWithWindows);
        RefreshConnectionStatus();
        ShowSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _wake?.Dismiss();
        _player?.Dispose();
        _query?.Dispose();
        _osc?.Dispose();
        _tray?.Dispose();
        _mutex?.Dispose();
        AppTheme.Stop();
        base.OnExit(e);
    }

    private void OnOscMessage(OscMessage message)
    {
        _receivedOsc = true;
        RefreshConnectionStatus();
        if (_touched.Observe(message.Address, message.FirstArgument))
        {
            _wake.RequestWake("osc");
        }
    }

    private void RefreshConnectionStatus()
    {
        var linked = _oscReady && (_receivedOsc || (_query?.IsVrChatAdvertised() ?? false));
        var text = linked ? "Linked with VRChat" : "Not linked with VRChat";
        if (text == _status)
        {
            return;
        }

        _status = text;
        _settingsWindow?.SetStatus(text);
    }

    private void RefreshTray()
    {
        _tray.SetState(_wake.Armed, _wake.IsPlaying);
        _settingsWindow?.SetArmed(_wake.Armed);
    }

    private void SetArmed(bool armed)
    {
        _settings.Armed = armed;
        _wake.Armed = armed;
        SaveSettings();
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
                _settings,
                _player.ListDevices(),
                _status,
                _wake.Armed,
                OnSettingsChanged,
                SetArmed);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "VRCWakeMe settings");
            return;
        }
        _settingsWindow.TestRequested += async () =>
        {
            try
            {
                await _player.PlayPreviewAsync(_settings, TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not play alarm: {ex.Message}", "VRCWakeMe");
            }
            finally
            {
                if (!_wake.IsPlaying)
                {
                    _player.Stop();
                }
            }
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        try
        {
            _settingsWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "VRCWakeMe settings");
        }
    }

    private void OnSettingsChanged()
    {
        _wake.Cooldown = TimeSpan.FromSeconds(_settings.CooldownSeconds);
        _wake.MaxDuration = TimeSpan.FromSeconds(_settings.MaxDurationSeconds);
        StartupRegistration.Apply(_settings.StartWithWindows);
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not save settings: {ex.Message}", "VRCWakeMe");
        }
    }
}
