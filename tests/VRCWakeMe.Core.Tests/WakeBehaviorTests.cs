using System.Buffers.Binary;
using System.Text;
using VRCWakeMe.Core;

namespace VRCWakeMe.Core.Tests;

public class WakeCoordinatorTests
{
    private static WakeCoordinator Create(bool armed = true) => new()
    {
        Armed = armed,
        Cooldown = TimeSpan.FromSeconds(20),
        MaxDuration = TimeSpan.FromSeconds(45)
    };

    [Fact]
    public void Disarmed_DoesNotStart()
    {
        var wake = Create(armed: false);
        var started = 0;
        wake.AlarmStarted += () => started++;

        var result = wake.RequestWake("osc", DateTimeOffset.UnixEpoch);

        Assert.Equal(WakeResult.Disarmed, result);
        Assert.False(wake.IsPlaying);
        Assert.Equal(0, started);
    }

    [Fact]
    public void FirstPoke_StartsAlarm()
    {
        var wake = Create();
        var started = 0;
        wake.AlarmStarted += () => started++;

        var result = wake.RequestWake("osc", DateTimeOffset.UnixEpoch);

        Assert.Equal(WakeResult.Started, result);
        Assert.True(wake.IsPlaying);
        Assert.Equal(1, started);
    }

    [Fact]
    public void SecondPokeWhilePlaying_IsAlreadyPlaying()
    {
        var wake = Create();
        var t0 = DateTimeOffset.UnixEpoch;
        wake.RequestWake("osc", t0);

        var result = wake.RequestWake("osc", t0.AddSeconds(1));

        Assert.Equal(WakeResult.AlreadyPlaying, result);
    }

    [Fact]
    public void PokeDuringCooldownAfterDismiss_IsOnCooldown()
    {
        var wake = Create();
        var t0 = DateTimeOffset.UnixEpoch;
        wake.RequestWake("osc", t0);
        wake.Dismiss();

        var result = wake.RequestWake("osc", t0.AddSeconds(5));

        Assert.Equal(WakeResult.OnCooldown, result);
        Assert.False(wake.IsPlaying);
    }

    [Fact]
    public void PokeAfterCooldown_StartsAgain()
    {
        var wake = Create();
        var t0 = DateTimeOffset.UnixEpoch;
        wake.RequestWake("osc", t0);
        wake.Dismiss();

        var result = wake.RequestWake("osc", t0.AddSeconds(20));

        Assert.Equal(WakeResult.Started, result);
        Assert.True(wake.IsPlaying);
    }

    [Fact]
    public void TickAfterMaxDuration_StopsAlarm()
    {
        var wake = Create();
        var stopped = 0;
        wake.AlarmStopped += () => stopped++;
        var t0 = DateTimeOffset.UnixEpoch;
        wake.RequestWake("osc", t0);

        wake.Tick(t0.AddSeconds(44));
        Assert.True(wake.IsPlaying);
        Assert.Equal(0, stopped);

        wake.Tick(t0.AddSeconds(45));
        Assert.False(wake.IsPlaying);
        Assert.Equal(1, stopped);
    }

    [Fact]
    public void Disarming_StopsPlayingAlarm()
    {
        var wake = Create();
        var stopped = 0;
        wake.AlarmStopped += () => stopped++;
        wake.RequestWake("osc", DateTimeOffset.UnixEpoch);

        wake.Armed = false;

        Assert.False(wake.IsPlaying);
        Assert.Equal(1, stopped);
    }

    [Fact]
    public void FutureWebSource_UsesSameHook()
    {
        IWakeTrigger trigger = Create();
        Assert.Equal(WakeResult.Started, trigger.RequestWake("web"));
    }
}

public class OscTouchedTrackerTests
{
    [Fact]
    public void RisingEdge_TriggersOnceWhileHeld()
    {
        var tracker = new OscTouchedTracker();

        Assert.False(tracker.Observe(OscAddresses.Touched, false));
        Assert.True(tracker.Observe(OscAddresses.Touched, true));
        Assert.False(tracker.Observe(OscAddresses.Touched, true));
        Assert.False(tracker.Observe(OscAddresses.Touched, 1));
        Assert.False(tracker.Observe(OscAddresses.Touched, false));
        Assert.True(tracker.Observe(OscAddresses.Touched, 1.0f));
    }

    [Fact]
    public void OtherAddresses_AreIgnored()
    {
        var tracker = new OscTouchedTracker();
        Assert.False(tracker.Observe("/avatar/parameters/Other", true));
        Assert.True(tracker.Observe(OscAddresses.Touched, true));
    }

    [Fact]
    public void OscValue_BoolAndInt()
    {
        Assert.True(OscValue.IsOn(true));
        Assert.False(OscValue.IsOn(false));
        Assert.True(OscValue.IsOn(1));
        Assert.False(OscValue.IsOn(0));
    }

    [Fact]
    public void OscValue_FloatThreshold()
    {
        Assert.False(OscValue.IsOn(0.49f));
        Assert.True(OscValue.IsOn(0.5f));
        Assert.True(OscValue.IsOn(1.0f));
    }
}

public class OscPacketParserTests
{
    [Fact]
    public void ParsesBoolTrue()
    {
        var bytes = OscTestMessage.Write(OscAddresses.Touched, "T");
        var messages = OscPacketParser.Parse(bytes);

        var message = Assert.Single(messages);
        Assert.Equal(OscAddresses.Touched, message.Address);
        Assert.Equal(true, message.FirstArgument);
    }

    [Fact]
    public void ParsesBoolFalse()
    {
        var bytes = OscTestMessage.Write(OscAddresses.Touched, "F");
        var message = Assert.Single(OscPacketParser.Parse(bytes));
        Assert.Equal(false, message.FirstArgument);
    }

    [Fact]
    public void ParsesInt()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, 1);
        var bytes = OscTestMessage.Write(OscAddresses.Touched, "i", payload);
        var message = Assert.Single(OscPacketParser.Parse(bytes));
        Assert.Equal(1, message.FirstArgument);
    }

    [Fact]
    public void ParsesFloat()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(payload, 1f);
        var bytes = OscTestMessage.Write(OscAddresses.Touched, "f", payload);
        var message = Assert.Single(OscPacketParser.Parse(bytes));
        Assert.Equal(1f, Assert.IsType<float>(message.FirstArgument));
    }

    [Fact]
    public void ParsesBundle()
    {
        var inner = OscTestMessage.Write(OscAddresses.Touched, "T");
        var bundle = OscTestMessage.WriteBundle(inner);
        var message = Assert.Single(OscPacketParser.Parse(bundle));
        Assert.Equal(true, message.FirstArgument);
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void RoundTripsSettings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "VRCWakeMeTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            var settings = new AppSettings
            {
                Armed = false,
                CooldownSeconds = 30,
                MaxDurationSeconds = 12,
                Volume = 0.4f,
                OutputDeviceName = "Headset",
                CustomSoundPath = @"C:\alarm.wav",
                StartWithWindows = true
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.False(loaded.Armed);
            Assert.Equal(30, loaded.CooldownSeconds);
            Assert.Equal(12, loaded.MaxDurationSeconds);
            Assert.Equal(0.4f, loaded.Volume);
            Assert.Equal("Headset", loaded.OutputDeviceName);
            Assert.Equal(@"C:\alarm.wav", loaded.CustomSoundPath);
            Assert.True(loaded.StartWithWindows);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}

internal static class OscTestMessage
{
    public static byte[] Write(string address, string typeTagWithoutComma, byte[]? argumentBytes = null)
    {
        using var ms = new MemoryStream();
        WritePadded(ms, address);
        WritePadded(ms, "," + typeTagWithoutComma);
        if (argumentBytes is { Length: > 0 })
        {
            ms.Write(argumentBytes, 0, argumentBytes.Length);
        }

        return ms.ToArray();
    }

    public static byte[] WriteBundle(params byte[][] messages)
    {
        using var ms = new MemoryStream();
        WritePadded(ms, "#bundle");
        ms.Write(new byte[8], 0, 8);
        foreach (var message in messages)
        {
            var size = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(size, message.Length);
            ms.Write(size, 0, 4);
            ms.Write(message, 0, message.Length);
        }

        return ms.ToArray();
    }

    private static void WritePadded(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0);
        var padded = bytes.Length + 1;
        while (padded % 4 != 0)
        {
            stream.WriteByte(0);
            padded++;
        }
    }
}
