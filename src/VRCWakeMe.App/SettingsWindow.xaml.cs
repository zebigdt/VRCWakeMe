using System.Windows;
using VRCWakeMe.App.Audio;
using VRCWakeMe.Core;

namespace VRCWakeMe.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onChanged;
    private bool _loading;

    public SettingsWindow(
        AppSettings settings,
        IReadOnlyList<AudioDeviceOption> devices,
        string statusText,
        Action onChanged)
    {
        InitializeComponent();
        _settings = settings;
        _onChanged = onChanged;

        DeviceCombo.ItemsSource = devices;
        StatusText.Text = statusText;

        BrowseSoundButton.Click += (_, _) => BrowseSound();
        ClearSoundButton.Click += (_, _) =>
        {
            SoundPathBox.Text = "";
            Persist();
        };
        TestButton.Click += (_, _) => TestRequested?.Invoke();
        CloseButton.Click += (_, _) => Close();
        DeviceCombo.SelectionChanged += (_, _) => Persist();
        VolumeSlider.ValueChanged += (_, _) =>
        {
            VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
            Persist();
        };
        CooldownBox.LostFocus += (_, _) => Persist();
        MaxDurationBox.LostFocus += (_, _) => Persist();
        StartWithWindowsCheck.Checked += (_, _) => Persist();
        StartWithWindowsCheck.Unchecked += (_, _) => Persist();

        LoadFromSettings(devices);
    }

    public event Action? TestRequested;

    public void SetStatus(string text) => StatusText.Text = text;

    private void LoadFromSettings(IReadOnlyList<AudioDeviceOption> devices)
    {
        _loading = true;
        var match = devices.FirstOrDefault(d =>
            string.Equals(d.Name, _settings.OutputDeviceName, StringComparison.Ordinal));
        DeviceCombo.SelectedItem = match ?? devices[0];

        VolumeSlider.Value = _settings.Volume * 100;
        VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        CooldownBox.Text = _settings.CooldownSeconds.ToString();
        MaxDurationBox.Text = _settings.MaxDurationSeconds.ToString();
        SoundPathBox.Text = _settings.CustomSoundPath ?? "";
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        _loading = false;
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
            SoundPathBox.Text = dialog.FileName;
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
        if (int.TryParse(CooldownBox.Text, out var cooldown))
        {
            _settings.CooldownSeconds = cooldown;
        }

        if (int.TryParse(MaxDurationBox.Text, out var maxDuration))
        {
            _settings.MaxDurationSeconds = maxDuration;
        }

        _settings.CustomSoundPath = string.IsNullOrWhiteSpace(SoundPathBox.Text) ? null : SoundPathBox.Text;
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _settings.Clamp();
        _onChanged();
    }
}
