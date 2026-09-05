using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace VRCWakeMe.App;

internal static class AppTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static bool IsLight { get; private set; } = true;

    public static void Start()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Stop()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    public static void ApplyToWindow(Window window)
    {
        var background = (System.Windows.Media.Brush)window.FindResource("BackgroundBrush");
        var foreground = (System.Windows.Media.Brush)window.FindResource("TextPrimaryBrush");
        window.Background = background;
        window.Foreground = foreground;
        window.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, foreground);
        window.Resources[System.Windows.SystemColors.WindowTextBrushKey] = foreground;
        window.Resources[System.Windows.SystemColors.ControlTextBrushKey] = foreground;
        window.Resources[System.Windows.SystemColors.GrayTextBrushKey] = (System.Windows.Media.Brush)window.FindResource("TextSecondaryBrush");
        window.Resources[System.Windows.SystemColors.WindowBrushKey] = background;
        ApplyTitleBar(window);
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() =>
        {
            Apply();
            var app = System.Windows.Application.Current;
            if (app is null)
            {
                return;
            }

            foreach (Window window in app.Windows)
            {
                ApplyToWindow(window);
            }
        });
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

        if (resources.MergedDictionaries.Count == 0)
        {
            resources.MergedDictionaries.Add(dict);
        }
        else
        {
            resources.MergedDictionaries[0] = dict;
        }
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
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value != 0;
            }
        }
        catch (Exception)
        {
        }

        return true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
