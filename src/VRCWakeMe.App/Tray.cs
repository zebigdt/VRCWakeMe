using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;

namespace VRCWakeMe.App;

internal static class SleepIcons
{
    private static readonly List<MemoryStream> IconStreams = [];
    private static readonly Color ArmedFill = Color.FromArgb(255, 22, 163, 74);
    private static readonly Color DisarmedFill = Color.FromArgb(255, 107, 114, 128);
    private static readonly Color Outline = Color.FromArgb(255, 124, 58, 237);

    public static Icon Armed { get; } = CreateIcon(ArmedFill, 32);
    public static Icon Disarmed { get; } = CreateIcon(DisarmedFill, 32);
    public static ImageSource ArmedImage { get; } = CreateImage(ArmedFill, 256);

    private static Icon CreateIcon(Color fill, int size)
    {
        using var bitmap = CreateBitmap(fill, size);
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        return PngToIcon(png.ToArray(), size);
    }

    private static BitmapSource CreateImage(Color fill, int size)
    {
        using var bitmap = CreateBitmap(fill, size);
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        png.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = png;
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
        graphics.Clear(Color.Transparent);
        var outlineWidth = Math.Max(2f, size * 0.07f);
        var inset = outlineWidth * 0.5f;
        using var path = RoundedRect(inset, inset, size - inset * 2, size - inset * 2, size * 0.2f);
        using var fillBrush = new SolidBrush(fill);
        using var outline = new Pen(Outline, outlineWidth) { LineJoin = LineJoin.Round, Alignment = PenAlignment.Center };
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
        path.AddPolygon(
        [
            new PointF(left, top), new PointF(right, top), new PointF(right, top + bar),
            new PointF(left + diagInset, bottom - bar), new PointF(right, bottom - bar),
            new PointF(right, bottom), new PointF(left, bottom), new PointF(left, bottom - bar),
            new PointF(right - diagInset, top + bar), new PointF(left, top + bar)
        ]);
        using var brush = new SolidBrush(Color.White);
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

    private static Icon PngToIcon(byte[] png, int size)
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
        IconStreams.Add(stream);
        return new Icon(stream);
    }
}

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _armedItem;
    private readonly ToolStripMenuItem _dismissItem;
    private readonly Icon _disarmedIcon = SleepIcons.Disarmed;
    private readonly Icon _armedIcon = SleepIcons.Armed;
    private bool _suppressArmedEvent;

    public TrayIcon()
    {
        _armedItem = new ToolStripMenuItem("Activated") { CheckOnClick = true };
        _armedItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressArmedEvent) ArmedChanged?.Invoke(_armedItem.Checked);
        };
        _dismissItem = new ToolStripMenuItem("Dismiss alarm", null, (_, _) => DismissRequested?.Invoke()) { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_armedItem);
        menu.Items.Add(_dismissItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _notify = new NotifyIcon
        {
            Icon = _disarmedIcon,
            Text = "VRCWakeMe",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notify.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenSettingsRequested?.Invoke();
        };
    }

    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;
    public event Action<bool>? ArmedChanged;
    public event Action? DismissRequested;

    public void SetState(bool armed, bool playing)
    {
        _suppressArmedEvent = true;
        _armedItem.Checked = armed;
        _suppressArmedEvent = false;
        _dismissItem.Enabled = playing;
        _notify.Icon = armed || playing ? _armedIcon : _disarmedIcon;
        _notify.Text = playing ? "VRCWakeMe — alarm" : armed ? "VRCWakeMe — activated" : "VRCWakeMe — inactive";
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _disarmedIcon.Dispose();
        _armedIcon.Dispose();
    }
}
