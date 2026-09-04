using System.Drawing;
using System.Windows.Forms;
using IOPath = System.IO.Path;

namespace VRCWakeMe.App;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _armedItem;
    private readonly ToolStripMenuItem _dismissItem;
    private readonly Icon _disarmedIcon;
    private readonly Icon _armedIcon;
    private readonly Icon _alarmingIcon;
    private bool _suppressArmedEvent;

    public TrayIcon()
    {
        var assets = IOPath.Combine(AppContext.BaseDirectory, "Assets");
        _disarmedIcon = new Icon(IOPath.Combine(assets, "disarmed.ico"));
        _armedIcon = new Icon(IOPath.Combine(assets, "armed.ico"));
        _alarmingIcon = new Icon(IOPath.Combine(assets, "alarming.ico"));

        _armedItem = new ToolStripMenuItem("Armed")
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
            _notify.Icon = _alarmingIcon;
            _notify.Text = "VRCWakeMe — alarm";
        }
        else if (armed)
        {
            _notify.Icon = _armedIcon;
            _notify.Text = "VRCWakeMe — armed";
        }
        else
        {
            _notify.Icon = _disarmedIcon;
            _notify.Text = "VRCWakeMe — disarmed";
        }
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _disarmedIcon.Dispose();
        _armedIcon.Dispose();
        _alarmingIcon.Dispose();
    }
}
