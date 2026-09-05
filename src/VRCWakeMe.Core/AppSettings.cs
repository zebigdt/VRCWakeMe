namespace VRCWakeMe.Core;

public sealed class AppSettings
{
    public bool Armed { get; set; } = false;
    public int CooldownSeconds { get; set; } = 60;
    public int MaxDurationSeconds { get; set; } = 30;
    public float Volume { get; set; } = 0.25f;
    public string? OutputDeviceName { get; set; }
    public string? CustomSoundPath { get; set; }
    public bool StartWithWindows { get; set; }

    public void Clamp()
    {
        CooldownSeconds = Math.Clamp(CooldownSeconds, 1, 600);
        MaxDurationSeconds = Math.Clamp(MaxDurationSeconds, 1, 600);
        Volume = Math.Clamp(Volume, 0.01f, 1f);
    }
}
