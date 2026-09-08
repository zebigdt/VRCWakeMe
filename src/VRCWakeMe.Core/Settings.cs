using System.Text.Json;
using System.Text.Json.Serialization;

namespace VRCWakeMe.Core;

public sealed class AppSettings
{
    public bool Armed { get; set; }
    public int CooldownSeconds { get; set; } = 60;
    public int MaxDurationSeconds { get; set; } = 30;
    public float Volume { get; set; } = 0.25f;
    public string? OutputDeviceName { get; set; }
    public string? CustomSoundPath { get; set; }
    public bool ForegroundOnAlarm { get; set; } = true;

    public void Clamp()
    {
        CooldownSeconds = Math.Clamp(CooldownSeconds, 1, 600);
        MaxDurationSeconds = Math.Clamp(MaxDurationSeconds, 1, 600);
        Volume = Math.Clamp(Volume, 0.01f, 1f);
    }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SettingsStore(string? directory = null)
    {
        var dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRCWakeMe");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, "settings.json");
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions)
                           ?? new AppSettings();
            settings.Clamp();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Clamp();
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
