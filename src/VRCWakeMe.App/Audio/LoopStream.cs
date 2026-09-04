using NAudio.Wave;

namespace VRCWakeMe.App.Audio;

internal sealed class LoopStream : WaveStream
{
    private readonly WaveStream _source;

    public LoopStream(WaveStream source)
    {
        _source = source;
        EnableLooping = true;
    }

    public bool EnableLooping { get; set; }

    public override WaveFormat WaveFormat => _source.WaveFormat;

    public override long Length => _source.Length;

    public override long Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = _source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                if (!EnableLooping || _source.Length == 0)
                {
                    break;
                }

                _source.Position = 0;
                continue;
            }

            total += read;
        }

        return total;
    }

    protected override void Dispose(bool disposing)
    {
        // AudioFileReader is owned by AlarmPlayer.
        base.Dispose(disposing);
    }
}
