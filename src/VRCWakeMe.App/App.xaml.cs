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
    private string _status = "Starting…";

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
            _status = $"OSCQuery as {OscQueryHost.ServiceName} — UDP {_osc.Port}, HTTP {_query.TcpPort}. VRChat should show a HUD notice when it starts sending.";
        }
        catch (Exception ex)
        {
            _status = $"OSC failed to start: {ex.Message}";
            System.Windows.MessageBox.Show(_status, "VRCWakeMe");
        }

        _tray = new TrayIcon();
        _tray.ArmedChanged += armed =>
        {
            _settings.Armed = armed;
            _wake.Armed = armed;
            SaveSettings();
        };
        _tray.DismissRequested += () => _wake.Dismiss();
        _tray.OpenSettingsRequested += ShowSettings;
        _tray.ExitRequested += Shutdown;
        RefreshTray();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => _wake.Tick();
        _timer.Start();

        StartupRegistration.Apply(_settings.StartWithWindows);
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
        base.OnExit(e);
    }

    private void OnOscMessage(OscMessage message)
    {
        if (_touched.Observe(message.Address, message.FirstArgument))
        {
            _wake.RequestWake("osc");
        }
    }

    private void RefreshTray() => _tray.SetState(_wake.Armed, _wake.IsPlaying);

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(
            _settings,
            _player.ListDevices(),
            _status,
            OnSettingsChanged);
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
        _settingsWindow.Show();
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
