using NAudio.Wave;
using System.IO;

namespace VRCWakeMe;

public sealed class AudioDeviceOption(int number, string name)
{
    public int Number { get; } = number;
    public string Name { get; } = name;
    public override string ToString() => Name;
}

internal sealed class LoopStream(WaveStream source) : WaveStream
{
    public bool EnableLooping { get; set; } = true;
    public override WaveFormat WaveFormat => source.WaveFormat;
    public override long Length => source.Length;
    public override long Position { get => source.Position; set => source.Position = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, offset + total, count - total);
            if (read != 0)
            {
                total += read;
                continue;
            }

            if (!EnableLooping || source.Length == 0) break;
            source.Position = 0;
        }

        return total;
    }
}

internal sealed class AlarmPlayer : IDisposable
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private LoopStream? _loop;

    public static string BundledAlarmPath => Path.Combine(AppContext.BaseDirectory, "Assets", "alarm.wav");

    public IReadOnlyList<AudioDeviceOption> ListDevices()
    {
        var devices = new List<AudioDeviceOption> { new(-1, "System default") };
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            devices.Add(new AudioDeviceOption(i, WaveOut.GetCapabilities(i).ProductName));
        }

        return devices;
    }

    public void Play(AppSettings settings, bool loop)
    {
        lock (_gate)
        {
            StopLocked();
            var path = !string.IsNullOrWhiteSpace(settings.CustomSoundPath) && System.IO.File.Exists(settings.CustomSoundPath)
                ? settings.CustomSoundPath!
                : BundledAlarmPath;
            _reader = new AudioFileReader(path) { Volume = settings.Volume };
            WaveStream source = _reader;
            if (loop)
            {
                _loop = new LoopStream(_reader);
                source = _loop;
            }

            _output = new WaveOutEvent { DeviceNumber = ResolveDevice(settings.OutputDeviceName) };
            _output.Init(source);
            _output.Play();
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    public void Dispose() => Stop();

    private void StopLocked()
    {
        try { _output?.Stop(); } catch (Exception) { }
        _output?.Dispose();
        _output = null;
        _loop?.Dispose();
        _loop = null;
        _reader?.Dispose();
        _reader = null;
    }

    private static int ResolveDevice(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || deviceName == "System default") return -1;
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
