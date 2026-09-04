using NAudio.Wave;
using VRCWakeMe.Core;
using IOPath = System.IO.Path;

namespace VRCWakeMe.App.Audio;

internal sealed class AlarmPlayer : IDisposable
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private LoopStream? _loop;
    private CancellationTokenSource? _previewCts;

    public static string BundledAlarmPath =>
        IOPath.Combine(AppContext.BaseDirectory, "Assets", "alarm.wav");

    public IReadOnlyList<AudioDeviceOption> ListDevices()
    {
        var devices = new List<AudioDeviceOption>
        {
            new(-1, "System default")
        };

        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add(new AudioDeviceOption(i, caps.ProductName));
        }

        return devices;
    }

    public void Play(AppSettings settings, bool loop)
    {
        lock (_gate)
        {
            StopLocked();
            var path = ResolveSoundPath(settings);
            _reader = new AudioFileReader(path)
            {
                Volume = settings.Volume
            };
            WaveStream source = _reader;
            if (loop)
            {
                _loop = new LoopStream(_reader);
                source = _loop;
            }

            _output = new WaveOutEvent
            {
                DeviceNumber = ResolveDeviceNumber(settings.OutputDeviceName)
            };
            _output.Init(source);
            _output.Play();
        }
    }

    public async Task PlayPreviewAsync(AppSettings settings, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            _previewCts?.Cancel();
            _previewCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts = _previewCts;
        }

        Play(settings, loop: true);
        try
        {
            await Task.Delay(duration, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // dismissed or a newer preview started
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_previewCts, cts))
                {
                    StopLocked();
                    _previewCts = null;
                }
            }

            cts.Dispose();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _previewCts?.Cancel();
            StopLocked();
        }
    }

    public void Dispose() => Stop();

    private void StopLocked()
    {
        try
        {
            _output?.Stop();
        }
        catch (Exception)
        {
            // device may already be gone
        }

        _output?.Dispose();
        _output = null;
        _loop?.Dispose();
        _loop = null;
        _reader?.Dispose();
        _reader = null;
    }

    private static string ResolveSoundPath(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CustomSoundPath) && System.IO.File.Exists(settings.CustomSoundPath))
        {
            return settings.CustomSoundPath!;
        }

        return BundledAlarmPath;
    }

    private static int ResolveDeviceNumber(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || deviceName == "System default")
        {
            return -1;
        }

        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            if (string.Equals(WaveOut.GetCapabilities(i).ProductName, deviceName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
