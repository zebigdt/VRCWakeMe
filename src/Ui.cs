using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace VRCWakeMe;

internal static class StartupRegistration
{
    private const string ValueName = "VRCWakeMe";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Start with Windows was removed. Builds that had it enabled left a Run entry
    /// behind, and there is no longer any UI to turn it off, so drop it.
    /// </summary>
    public static void ClearLegacyEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) != null) key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception)
        {
        }
    }
}

internal static class WindowForeground
{
    private const int SwRestore = 9;

    public static void Bring(Window window)
    {
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        if (!window.IsVisible) window.Show();

        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SwRestore);
            SetForegroundWindow(handle);
        }

        // Windows refuses focus to background processes, so lift the window above
        // the game too. That keeps it clickable even when the activate call loses.
        window.Topmost = true;
        window.Topmost = false;
        window.Activate();
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}

internal static class NumericTextBox
{
    public static void Attach(System.Windows.Controls.TextBox box, Action onCommitted, int min, int max)
    {
        InputMethod.SetIsInputMethodEnabled(box, false);
        box.PreviewTextInput += (_, e) => e.Handled = !IsDigits(e.Text);
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Space) e.Handled = true;
            else if (e.Key is Key.Up) { Nudge(box, 1, min, max, onCommitted); e.Handled = true; }
            else if (e.Key is Key.Down) { Nudge(box, -1, min, max, onCommitted); e.Handled = true; }
        };
        box.MouseWheel += (_, e) =>
        {
            Nudge(box, e.Delta > 0 ? 1 : -1, min, max, onCommitted);
            e.Handled = true;
        };
        System.Windows.DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text) ||
                !IsDigits(e.DataObject.GetData(System.Windows.DataFormats.Text) as string ?? ""))
            {
                e.CancelCommand();
            }
        });
        box.LostFocus += (_, _) => onCommitted();
    }

    public static int Read(System.Windows.Controls.TextBox box, int fallback, int min, int max)
    {
        if (!int.TryParse(box.Text, out var value))
        {
            box.Text = fallback.ToString();
            return fallback;
        }

        var clamped = Math.Clamp(value, min, max);
        box.Text = clamped.ToString();
        return clamped;
    }

    public static void Nudge(System.Windows.Controls.TextBox box, int delta, int min, int max, Action onCommitted)
    {
        if (!int.TryParse(box.Text, out var value)) value = delta > 0 ? min - 1 : min;
        var next = Math.Clamp(value + delta, min, max);
        if (box.Text == next.ToString()) return;
        box.Text = next.ToString();
        onCommitted();
    }

    private static bool IsDigits(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }

        return text.Length > 0;
    }
}

internal static class AppTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static bool IsLight { get; private set; } = true;

    public static void Start()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Stop() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    public static void ApplyToWindow(Window window)
    {
        var background = (System.Windows.Media.Brush)window.FindResource("BackgroundBrush");
        var foreground = (System.Windows.Media.Brush)window.FindResource("TextPrimaryBrush");
        window.Background = background;
        window.Foreground = foreground;
        window.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, foreground);
        window.Resources[System.Windows.SystemColors.WindowTextBrushKey] = foreground;
        window.Resources[System.Windows.SystemColors.ControlTextBrushKey] = foreground;
        window.Resources[System.Windows.SystemColors.GrayTextBrushKey] =
            (System.Windows.Media.Brush)window.FindResource("TextSecondaryBrush");
        window.Resources[System.Windows.SystemColors.WindowBrushKey] = background;
        ApplyTitleBar(window);
    }

    public static void Apply()
    {
        IsLight = ReadAppsUseLightTheme();
        var resources = System.Windows.Application.Current.Resources;
        var dict = new ResourceDictionary
        {
            Source = new Uri(IsLight
                ? "pack://application:,,,/Themes/Light.xaml"
                : "pack://application:,,,/Themes/Dark.xaml")
        };
        if (resources.MergedDictionaries.Count == 0) resources.MergedDictionaries.Add(dict);
        else resources.MergedDictionaries[0] = dict;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Apply();
            var app = System.Windows.Application.Current;
            if (app is null) return;
            foreach (Window window in app.Windows) ApplyToWindow(window);
        });
    }

    private static void ApplyTitleBar(Window window)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => ApplyTitleBar(window);
            return;
        }

        var useDark = IsLight ? 0 : 1;
        if (DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(helper.Handle, 19, ref useDark, sizeof(int));
        }
    }

    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value) return value != 0;
        }
        catch (Exception)
        {
        }

        return true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
