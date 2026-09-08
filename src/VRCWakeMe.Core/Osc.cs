using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace VRCWakeMe.Core;

public readonly record struct OscMessage(string Address, IReadOnlyList<object?> Arguments)
{
    public object? FirstArgument => Arguments.Count > 0 ? Arguments[0] : null;
}

public readonly record struct OscGrab(
    string Handle,
    bool Grabbed,
    bool Pulled,
    bool IsHandle,
    bool Changed,
    bool Wake);

public static class OscAddresses
{
    public const string Parameters = "/avatar/parameters/";
    public const string GrabbedSuffix = "_IsGrabbed";
    public const string StretchSuffix = "_Stretch";
    public const string Handle = "WakeMe";
    public const string Grabbed = Parameters + Handle + GrabbedSuffix;
    public const string Stretch = Parameters + Handle + StretchSuffix;
    public const string DebugArmed = "/VRCWakeMe/armed";
    public const string DebugDisarmed = "/VRCWakeMe/disarmed";
    public const string DebugGrabbed = "/VRCWakeMe/grabbed";
    public const string DebugPulled = "/VRCWakeMe/pulled";
}

public static class OscValue
{
    public static bool IsOn(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => s is "1" or "true" or "True" or "TRUE",
        float f => f >= 0.5f,
        double d => d >= 0.5,
        decimal m => m >= 0.5m,
        IConvertible c => Convert.ToInt64(c) != 0,
        _ => false
    };

    public static float AsFloat(object? value) => value switch
    {
        null => 0f,
        bool b => b ? 1f : 0f,
        float f => f,
        double d => (float)d,
        string s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f,
        IConvertible c => Convert.ToSingle(c, CultureInfo.InvariantCulture),
        _ => 0f
    };
}

/// <summary>
/// Tracks PhysBone grab handles. A poke cannot move these, only a deliberate
/// grip-grab, and when the handle also reports stretch the grab has to be pulled
/// before it counts as a wake. That is what keeps a brush past the avatar quiet.
/// </summary>
public sealed class OscGrabTracker
{
    /// <summary>How far through the PhysBone's stretch range a pull has to reach.</summary>
    public const float PullThreshold = 0.15f;

    private readonly Dictionary<string, Handle> _handles = new(StringComparer.Ordinal);

    public bool AnyGrabbed => _handles.Values.Any(handle => handle.Grabbed);

    public bool AnyPulled => _handles.Values.Any(handle => handle.Pulled);

    public OscGrab Observe(string address, object? value)
    {
        if (!address.StartsWith(OscAddresses.Parameters, StringComparison.Ordinal)) return default;

        var name = address[OscAddresses.Parameters.Length..];
        var isGrab = name.EndsWith(OscAddresses.GrabbedSuffix, StringComparison.Ordinal);
        var isStretch = !isGrab && name.EndsWith(OscAddresses.StretchSuffix, StringComparison.Ordinal);
        if (!isGrab && !isStretch) return default;

        var suffix = isGrab ? OscAddresses.GrabbedSuffix : OscAddresses.StretchSuffix;
        var handleName = name[..^suffix.Length];
        if (handleName.Length == 0) return default;

        _handles.TryGetValue(handleName, out var handle);
        var wasGrabbed = handle.Grabbed;
        var wasPulled = handle.Pulled;

        if (isGrab)
        {
            handle.Grabbed = OscValue.IsOn(value);

            // A released bone springs back to rest, so drop the last stretch we saw
            // rather than letting a stale one count as the next grab's pull.
            if (!handle.Grabbed) handle.Stretch = 0f;
        }
        else
        {
            handle.Stretch = OscValue.AsFloat(value);
            handle.ReportsStretch = true;
        }

        handle.Pulled = handle.Grabbed && (!handle.ReportsStretch || handle.Stretch >= PullThreshold);
        _handles[handleName] = handle;

        return new OscGrab(
            handleName,
            handle.Grabbed,
            handle.Pulled,
            IsHandle: true,
            Changed: handle.Grabbed != wasGrabbed || handle.Pulled != wasPulled,
            Wake: handle.Pulled && !wasPulled);
    }

    public void Reset() => _handles.Clear();

    private struct Handle
    {
        public bool Grabbed;
        public bool ReportsStretch;
        public float Stretch;
        public bool Pulled;
    }
}

public static class OscWriter
{
    public static byte[] Write(string address, params object[] args)
    {
        using var values = new MemoryStream();
        var tags = new StringBuilder(",");
        foreach (var arg in args)
        {
            switch (arg)
            {
                case bool b:
                    tags.Append(b ? 'T' : 'F');
                    break;
                case int i:
                    tags.Append('i');
                    WriteInt(values, i);
                    break;
                case float f:
                    tags.Append('f');
                    WriteFloat(values, f);
                    break;
                case string s:
                    tags.Append('s');
                    WritePadded(values, s);
                    break;
                default:
                    throw new ArgumentException($"Unsupported OSC type {arg?.GetType().Name ?? "null"}");
            }
        }

        using var packet = new MemoryStream();
        WritePadded(packet, address);
        WritePadded(packet, tags.ToString());
        packet.Write(values.ToArray());
        return packet.ToArray();
    }

    private static void WriteInt(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteFloat(Stream stream, float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void WritePadded(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
        stream.WriteByte(0);
        var padded = bytes.Length + 1;
        while (padded % 4 != 0)
        {
            stream.WriteByte(0);
            padded++;
        }
    }
}

public static class OscPacketParser
{
    public static IReadOnlyList<OscMessage> Parse(ReadOnlySpan<byte> data)
    {
        var messages = new List<OscMessage>();
        ParseInto(data, messages);
        return messages;
    }

    public static IReadOnlyList<OscMessage> Parse(byte[] data) => Parse(data.AsSpan());

    private static void ParseInto(ReadOnlySpan<byte> data, List<OscMessage> messages)
    {
        if (data.Length < 4)
        {
            return;
        }

        if (IsBundle(data))
        {
            ParseBundle(data, messages);
            return;
        }

        if (TryParseMessage(data, out var message))
        {
            messages.Add(message);
        }
    }

    private static bool IsBundle(ReadOnlySpan<byte> data) =>
        data.Length >= 8 &&
        data[0] == (byte)'#' && data[1] == (byte)'b' && data[2] == (byte)'u' &&
        data[3] == (byte)'n' && data[4] == (byte)'d' && data[5] == (byte)'l' &&
        data[6] == (byte)'e' && data[7] == 0;

    private static void ParseBundle(ReadOnlySpan<byte> data, List<OscMessage> messages)
    {
        var offset = 16;
        while (offset + 4 <= data.Length)
        {
            var size = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            offset += 4;
            if (size < 0 || offset + size > data.Length)
            {
                return;
            }

            ParseInto(data.Slice(offset, size), messages);
            offset += size;
        }
    }

    private static bool TryParseMessage(ReadOnlySpan<byte> data, out OscMessage message)
    {
        message = default;
        var offset = 0;
        if (!TryReadString(data, ref offset, out var address) || address.Length == 0 || address[0] != '/')
        {
            return false;
        }

        var arguments = new List<object?>();
        if (offset >= data.Length || !TryReadString(data, ref offset, out var typeTag) || typeTag.Length == 0)
        {
            message = new OscMessage(address, arguments);
            return true;
        }

        var tags = typeTag[0] == ',' ? typeTag.AsSpan(1) : typeTag.AsSpan();
        foreach (var tag in tags)
        {
            if (!TryReadArgument(data, ref offset, tag, out var value))
            {
                break;
            }

            arguments.Add(value);
        }

        message = new OscMessage(address, arguments);
        return true;
    }

    private static bool TryReadArgument(ReadOnlySpan<byte> data, ref int offset, char tag, out object? value)
    {
        value = null;
        switch (tag)
        {
            case 'T':
            case 'I':
                value = true;
                return true;
            case 'F':
            case 'N':
                value = tag == 'F' ? false : null;
                return true;
            case 'i':
                if (offset + 4 > data.Length) return false;
                value = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
                offset += 4;
                return true;
            case 'f':
                if (offset + 4 > data.Length) return false;
                value = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset, 4));
                offset += 4;
                return true;
            case 'h':
                if (offset + 8 > data.Length) return false;
                value = BinaryPrimitives.ReadInt64BigEndian(data.Slice(offset, 8));
                offset += 8;
                return true;
            case 'd':
                if (offset + 8 > data.Length) return false;
                value = BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset, 8));
                offset += 8;
                return true;
            case 's':
            case 'S':
                return TryReadString(data, ref offset, out var s) && (value = s) != null;
            case 'b':
                return TryReadBlob(data, ref offset, out value);
            default:
                return false;
        }
    }

    private static bool TryReadBlob(ReadOnlySpan<byte> data, ref int offset, out object? value)
    {
        value = null;
        if (offset + 4 > data.Length) return false;
        var size = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
        offset += 4;
        if (size < 0 || offset + size > data.Length) return false;
        value = data.Slice(offset, size).ToArray();
        offset += size;
        while (offset % 4 != 0) offset++;
        return true;
    }

    private static bool TryReadString(ReadOnlySpan<byte> data, ref int offset, out string value)
    {
        value = "";
        var start = offset;
        while (offset < data.Length && data[offset] != 0) offset++;
        if (offset >= data.Length) return false;
        value = Encoding.ASCII.GetString(data.Slice(start, offset - start));
        offset++;
        while (offset % 4 != 0) offset++;
        return true;
    }
}
