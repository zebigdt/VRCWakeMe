using System.Drawing;
using System.Windows.Forms;

namespace VRCWakeMe.App;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _armedItem;
    private readonly ToolStripMenuItem _dismissItem;
    private readonly Icon _disarmedIcon;
    private readonly Icon _armedIcon;
    private bool _suppressArmedEvent;

    public TrayIcon()
    {
        _disarmedIcon = SleepIcons.Disarmed;
        _armedIcon = SleepIcons.Armed;

        _armedItem = new ToolStripMenuItem("Activated")
        {
            CheckOnClick = true
        };
        _armedItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressArmedEvent)
            {
                ArmedChanged?.Invoke(_armedItem.Checked);
            }
        };

        _dismissItem = new ToolStripMenuItem("Dismiss alarm", null, (_, _) => DismissRequested?.Invoke())
        {
            Enabled = false
        };

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
            if (e.Button == MouseButtons.Left)
            {
                OpenSettingsRequested?.Invoke();
            }
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

        if (playing)
        {
            _notify.Icon = _armedIcon;
            _notify.Text = "VRCWakeMe — alarm";
        }
        else if (armed)
        {
            _notify.Icon = _armedIcon;
            _notify.Text = "VRCWakeMe — activated";
        }
        else
        {
            _notify.Icon = _disarmedIcon;
            _notify.Text = "VRCWakeMe — inactive";
        }
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _disarmedIcon.Dispose();
        _armedIcon.Dispose();
    }
}
