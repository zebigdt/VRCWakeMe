using System.Windows;
using VRCWakeMe.App.Audio;
using VRCWakeMe.Core;
using IOPath = System.IO.Path;

namespace VRCWakeMe.App;

public partial class SettingsWindow : Window
{
    private const int MinSeconds = 1;
    private const int MaxSeconds = 600;

    private readonly AppSettings _settings;
    private readonly Action _onChanged;
    private readonly Action<bool> _onArmedChanged;
    private string? _customSoundPath;
    private bool _loading;

    public SettingsWindow(
        AppSettings settings,
        IReadOnlyList<AudioDeviceOption> devices,
        string statusText,
        bool armed,
        Action onChanged,
        Action<bool> onArmedChanged)
    {
        InitializeComponent();
        Icon = SleepIcons.ArmedImage;
        AppTheme.ApplyToWindow(this);
        _settings = settings;
        _onChanged = onChanged;
        _onArmedChanged = onArmedChanged;

        DeviceCombo.ItemsSource = devices;
        StatusText.Text = statusText;

        NumericTextBox.Attach(CooldownBox, Persist);
        NumericTextBox.Attach(MaxDurationBox, Persist);

        BrowseSoundButton.Click += (_, _) => BrowseSound();
        ClearSoundButton.Click += (_, _) =>
        {
            _customSoundPath = null;
            RefreshSoundUi();
            Persist();
        };
        TestButton.Click += (_, _) => TestRequested?.Invoke();
        DeviceCombo.SelectionChanged += (_, _) => Persist();
        VolumeSlider.ValueChanged += (_, _) =>
        {
            VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
            Persist();
        };
        StartWithWindowsCheck.Checked += (_, _) => Persist();
        StartWithWindowsCheck.Unchecked += (_, _) => Persist();
        ArmedToggle.Checked += (_, _) => OnArmedToggle(true);
        ArmedToggle.Unchecked += (_, _) => OnArmedToggle(false);

        LoadFromSettings(devices, armed);
    }

    public event Action? TestRequested;

    public void SetStatus(string text) => StatusText.Text = text;

    public void SetArmed(bool armed)
    {
        if (ArmedToggle.IsChecked == armed)
        {
            return;
        }

        _loading = true;
        ArmedToggle.IsChecked = armed;
        UpdateArmedLabel(armed);
        _loading = false;
    }

    private void LoadFromSettings(IReadOnlyList<AudioDeviceOption> devices, bool armed)
    {
        _loading = true;
        var match = devices.FirstOrDefault(d =>
            string.Equals(d.Name, _settings.OutputDeviceName, StringComparison.Ordinal));
        DeviceCombo.SelectedItem = match ?? devices[0];

        var volumePercent = Math.Clamp(_settings.Volume * 100f, 1f, 100f);
        VolumeSlider.Value = volumePercent;
        VolumeLabel.Text = $"{(int)volumePercent}%";
        CooldownBox.Text = _settings.CooldownSeconds.ToString();
        MaxDurationBox.Text = _settings.MaxDurationSeconds.ToString();
        _customSoundPath = _settings.CustomSoundPath;
        RefreshSoundUi();
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        ArmedToggle.IsChecked = armed;
        UpdateArmedLabel(armed);
        _loading = false;
    }

    private void OnArmedToggle(bool armed)
    {
        if (_loading)
        {
            return;
        }

        UpdateArmedLabel(armed);
        _onArmedChanged(armed);
    }

    private void UpdateArmedLabel(bool armed)
    {
        ArmedTitle.Text = armed ? "Activated" : "Inactive";
    }

    private void RefreshSoundUi()
    {
        var usingCustom = !string.IsNullOrWhiteSpace(_customSoundPath);
        SoundNameBox.Text = usingCustom
            ? IOPath.GetFileName(_customSoundPath)
            : IOPath.GetFileName(AlarmPlayer.BundledAlarmPath);
        ClearSoundButton.Visibility = usingCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseSound()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio files|*.wav;*.mp3;*.wma;*.aiff;*.aif|All files|*.*",
            Title = "Choose alarm sound"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _customSoundPath = dialog.FileName;
            RefreshSoundUi();
            Persist();
        }
    }

    private void Persist()
    {
        if (_loading)
        {
            return;
        }

        if (DeviceCombo.SelectedItem is AudioDeviceOption device)
        {
            _settings.OutputDeviceName = device.Number < 0 ? null : device.Name;
        }

        _settings.Volume = (float)(VolumeSlider.Value / 100.0);
        _settings.CooldownSeconds = NumericTextBox.Read(CooldownBox, _settings.CooldownSeconds, MinSeconds, MaxSeconds);
        _settings.MaxDurationSeconds = NumericTextBox.Read(MaxDurationBox, _settings.MaxDurationSeconds, MinSeconds, MaxSeconds);
        _settings.CustomSoundPath = _customSoundPath;
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _settings.Clamp();
        _onChanged();
    }
}
