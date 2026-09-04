using System.Buffers.Binary;
using System.Text;

namespace VRCWakeMe.Core;

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
        data[0] == (byte)'#' &&
        data[1] == (byte)'b' &&
        data[2] == (byte)'u' &&
        data[3] == (byte)'n' &&
        data[4] == (byte)'d' &&
        data[5] == (byte)'l' &&
        data[6] == (byte)'e' &&
        data[7] == 0;

    private static void ParseBundle(ReadOnlySpan<byte> data, List<OscMessage> messages)
    {
        var offset = 16; // "#bundle\0" + 8-byte timetag
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
        if (offset >= data.Length)
        {
            message = new OscMessage(address, arguments);
            return true;
        }

        if (!TryReadString(data, ref offset, out var typeTag) || typeTag.Length == 0)
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
                value = true;
                return true;
            case 'F':
                value = false;
                return true;
            case 'N':
                value = null;
                return true;
            case 'I':
                value = true;
                return true;
            case 'i':
                if (offset + 4 > data.Length)
                {
                    return false;
                }

                value = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
                offset += 4;
                return true;
            case 'f':
                if (offset + 4 > data.Length)
                {
                    return false;
                }

                value = BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset, 4));
                offset += 4;
                return true;
            case 'h':
                if (offset + 8 > data.Length)
                {
                    return false;
                }

                value = BinaryPrimitives.ReadInt64BigEndian(data.Slice(offset, 8));
                offset += 8;
                return true;
            case 'd':
                if (offset + 8 > data.Length)
                {
                    return false;
                }

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
        if (offset + 4 > data.Length)
        {
            return false;
        }

        var size = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
        offset += 4;
        if (size < 0 || offset + size > data.Length)
        {
            return false;
        }

        value = data.Slice(offset, size).ToArray();
        offset += size;
        while (offset % 4 != 0)
        {
            offset++;
        }

        return true;
    }

    private static bool TryReadString(ReadOnlySpan<byte> data, ref int offset, out string value)
    {
        value = "";
        var start = offset;
        while (offset < data.Length && data[offset] != 0)
        {
            offset++;
        }

        if (offset >= data.Length)
        {
            return false;
        }

        value = Encoding.ASCII.GetString(data.Slice(start, offset - start));
        offset++;
        while (offset % 4 != 0)
        {
            offset++;
        }

        return true;
    }
}
