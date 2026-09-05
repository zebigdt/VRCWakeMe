using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;

namespace VRCWakeMe.App;

internal static class SleepIcons
{
    private static readonly List<MemoryStream> _iconStreams = new();
    private static readonly Color ArmedFill = Color.FromArgb(255, 22, 163, 74);
    private static readonly Color DisarmedFill = Color.FromArgb(255, 107, 114, 128);
    private static readonly Color Outline = Color.FromArgb(255, 124, 58, 237);
    private static readonly Color Glyph = Color.White;

    public static System.Drawing.Icon Armed { get; } = CreateIcon(ArmedFill, 32);
    public static System.Drawing.Icon Disarmed { get; } = CreateIcon(DisarmedFill, 32);
    public static ImageSource ArmedImage { get; } = CreateImageSource(ArmedFill, 256);

    private static System.Drawing.Icon CreateIcon(Color fill, int size)
    {
        using var bitmap = CreateBitmap(fill, size);
        var png = BitmapToPng(bitmap);
        return PngToIcon(png, size);
    }

    private static BitmapSource CreateImageSource(Color fill, int size)
    {
        using var bitmap = CreateBitmap(fill, size);
        var png = BitmapToPng(bitmap);
        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    internal static Bitmap CreateBitmap(Color fill, int size)
    {
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.Clear(Color.Transparent);

        var outlineWidth = Math.Max(2.0f, size * 0.07f);
        var inset = outlineWidth * 0.5f;
        using var path = RoundedRect(inset, inset, size - inset * 2, size - inset * 2, size * 0.2f);
        using var fillBrush = new SolidBrush(fill);
        using var outline = new Pen(Outline, outlineWidth)
        {
            LineJoin = LineJoin.Round,
            Alignment = PenAlignment.Center
        };
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(outline, path);
        graphics.SetClip(path);
        DrawZ(graphics, size);
        graphics.ResetClip();
        return bitmap;
    }

    private static void DrawZ(Graphics graphics, float size)
    {
        var left = size * 0.30f;
        var right = size * 0.70f;
        var top = size * 0.30f;
        var bottom = size * 0.70f;
        var bar = Math.Max(2.8f, size * 0.10f);
        var innerGap = Math.Max(1f, (bottom - top) - 2f * bar);
        var diagInset = Math.Min((right - left) * 0.55f, bar * ((right - left) / innerGap));

        using var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new PointF(left, top),
            new PointF(right, top),
            new PointF(right, top + bar),
            new PointF(left + diagInset, bottom - bar),
            new PointF(right, bottom - bar),
            new PointF(right, bottom),
            new PointF(left, bottom),
            new PointF(left, bottom - bar),
            new PointF(right - diagInset, top + bar),
            new PointF(left, top + bar)
        });
        using var brush = new SolidBrush(Glyph);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(width, height));
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static byte[] BitmapToPng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static System.Drawing.Icon PngToIcon(byte[] png, int size)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(22);
            writer.Write(png);
        }

        stream.Position = 0;
        _iconStreams.Add(stream);
        return new System.Drawing.Icon(stream);
    }
}
