using Microsoft.Win32;

namespace VRCWakeMe.App;

internal static class StartupRegistration
{
    private const string ValueName = "VRCWakeMe";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            key.SetValue(ValueName, $"\"{path}\"");
        }
        else if (key.GetValue(ValueName) != null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
