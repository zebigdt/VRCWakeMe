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
    public void FirstGrab_StartsAlarm()
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
    public void SecondGrabWhilePlaying_IsAlreadyPlaying()
    {
        var wake = Create();
        var t0 = DateTimeOffset.UnixEpoch;
        wake.RequestWake("osc", t0);

        var result = wake.RequestWake("osc", t0.AddSeconds(1));

        Assert.Equal(WakeResult.AlreadyPlaying, result);
    }

    [Fact]
    public void GrabDuringCooldownAfterDismiss_IsOnCooldown()
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
    public void GrabAfterCooldown_StartsAgain()
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

public class OscGrabTrackerTests
{
    [Fact]
    public void GrabWithoutStretchParameter_WakesOnRisingEdge()
    {
        var tracker = new OscGrabTracker();

        Assert.False(tracker.Observe(OscAddresses.Grabbed, false).Wake);
        Assert.True(tracker.Observe(OscAddresses.Grabbed, true).Wake);
        Assert.False(tracker.Observe(OscAddresses.Grabbed, true).Wake);
        Assert.False(tracker.Observe(OscAddresses.Grabbed, false).Wake);
        Assert.True(tracker.Observe(OscAddresses.Grabbed, 1.0f).Wake);
    }

    [Fact]
    public void GrabWithoutPull_DoesNotWake()
    {
        var tracker = new OscGrabTracker();
        tracker.Observe(OscAddresses.Stretch, 0f);

        var grab = tracker.Observe(OscAddresses.Grabbed, true);

        Assert.True(grab.Grabbed);
        Assert.False(grab.Pulled);
        Assert.False(grab.Wake);
        Assert.True(tracker.AnyGrabbed);
        Assert.False(tracker.AnyPulled);
    }

    [Fact]
    public void PullWhileGrabbed_WakesOnce()
    {
        var tracker = new OscGrabTracker();
        tracker.Observe(OscAddresses.Stretch, 0f);
        tracker.Observe(OscAddresses.Grabbed, true);

        Assert.False(tracker.Observe(OscAddresses.Stretch, 0.1f).Wake);
        Assert.True(tracker.Observe(OscAddresses.Stretch, 0.4f).Wake);
        Assert.True(tracker.AnyPulled);
        Assert.False(tracker.Observe(OscAddresses.Stretch, 0.9f).Wake);

        var released = tracker.Observe(OscAddresses.Grabbed, false);
        Assert.True(released.Changed);
        Assert.False(released.Pulled);
        Assert.False(tracker.AnyGrabbed);
        Assert.False(tracker.AnyPulled);
    }

    [Fact]
    public void RegrabAfterAPull_NeedsANewPull()
    {
        var tracker = new OscGrabTracker();
        tracker.Observe(OscAddresses.Stretch, 0f);
        tracker.Observe(OscAddresses.Grabbed, true);
        Assert.True(tracker.Observe(OscAddresses.Stretch, 0.5f).Wake);
        tracker.Observe(OscAddresses.Grabbed, false);

        Assert.False(tracker.Observe(OscAddresses.Grabbed, true).Wake);
        Assert.True(tracker.Observe(OscAddresses.Stretch, 0.5f).Wake);
    }

    [Fact]
    public void StretchWithoutGrab_DoesNotWake()
    {
        var tracker = new OscGrabTracker();

        var stretched = tracker.Observe(OscAddresses.Stretch, 1f);

        Assert.True(stretched.IsHandle);
        Assert.False(stretched.Wake);
        Assert.False(tracker.AnyPulled);
    }

    [Fact]
    public void PokesAndUnrelatedParameters_AreIgnored()
    {
        var tracker = new OscGrabTracker();

        foreach (var address in new[]
                 {
                     "/avatar/parameters/Viseme",
                     "/avatar/parameters/WakeMe",
                     "/avatar/parameters/HeadContact",
                     "/avatar/parameters/HeadTouch"
                 })
        {
            var observed = tracker.Observe(address, true);
            Assert.False(observed.IsHandle);
            Assert.False(observed.Wake);
        }

        Assert.False(tracker.AnyGrabbed);
    }

    [Fact]
    public void HandlesAreTrackedSeparately()
    {
        var tracker = new OscGrabTracker();
        tracker.Observe(OscAddresses.Stretch, 0f);
        tracker.Observe("/avatar/parameters/Hair_Stretch", 0.9f);

        // The wake handle is held but never pulled, so its own grab stays quiet.
        Assert.False(tracker.Observe(OscAddresses.Grabbed, true).Pulled);

        var hair = tracker.Observe("/avatar/parameters/Hair_IsGrabbed", true);
        Assert.Equal("Hair", hair.Handle);
        Assert.True(hair.Wake);
    }

    [Fact]
    public void OscValue_AsFloat()
    {
        Assert.Equal(0f, OscValue.AsFloat(null));
        Assert.Equal(1f, OscValue.AsFloat(true));
        Assert.Equal(0.25f, OscValue.AsFloat(0.25f));
        Assert.Equal(1f, OscValue.AsFloat(1));
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
        var bytes = OscTestMessage.Write(OscAddresses.Grabbed, "T");
        var messages = OscPacketParser.Parse(bytes);

        var message = Assert.Single(messages);
        Assert.Equal(OscAddresses.Grabbed, message.Address);
        Assert.Equal(true, message.FirstArgument);
    }

    [Fact]
    public void ParsesBoolFalse()
    {
        var bytes = OscTestMessage.Write(OscAddresses.Grabbed, "F");
        var message = Assert.Single(OscPacketParser.Parse(bytes));
        Assert.Equal(false, message.FirstArgument);
    }

    [Fact]
    public void ParsesInt()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, 1);
        var bytes = OscTestMessage.Write(OscAddresses.Grabbed, "i", payload);
        var message = Assert.Single(OscPacketParser.Parse(bytes));
        Assert.Equal(1, message.FirstArgument);
    }

    [Fact]
    public void ParsesFloat()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(payload, 1f);
        var bytes = OscTestMessage.Write(OscAddresses.Grabbed, "f", payload);
        var message = Assert.Single(OscPacketParser.Parse(bytes));
        Assert.Equal(1f, Assert.IsType<float>(message.FirstArgument));
    }

    [Fact]
    public void ParsesBundle()
    {
        var inner = OscTestMessage.Write(OscAddresses.Grabbed, "T");
        var bundle = OscTestMessage.WriteBundle(inner);
        var message = Assert.Single(OscPacketParser.Parse(bundle));
        Assert.Equal(true, message.FirstArgument);
    }

    [Fact]
    public void Writer_RoundTripsBoolAndString()
    {
        var grabbed = Assert.Single(OscPacketParser.Parse(OscWriter.Write(OscAddresses.Grabbed, true)));
        Assert.Equal(OscAddresses.Grabbed, grabbed.Address);
        Assert.Equal(true, grabbed.FirstArgument);

        var text = Assert.Single(OscPacketParser.Parse(OscWriter.Write("/test", "hello", true, false)));
        Assert.Equal("/test", text.Address);
        Assert.Equal("hello", text.Arguments[0]);
        Assert.Equal(true, text.Arguments[1]);
        Assert.Equal(false, text.Arguments[2]);
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
                ForegroundOnAlarm = false
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.False(loaded.Armed);
            Assert.Equal(30, loaded.CooldownSeconds);
            Assert.Equal(12, loaded.MaxDurationSeconds);
            Assert.Equal(0.4f, loaded.Volume);
            Assert.Equal("Headset", loaded.OutputDeviceName);
            Assert.Equal(@"C:\alarm.wav", loaded.CustomSoundPath);
            Assert.False(loaded.ForegroundOnAlarm);
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
