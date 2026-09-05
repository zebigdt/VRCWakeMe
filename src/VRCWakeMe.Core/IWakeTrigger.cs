namespace VRCWakeMe.Core;

/// <summary>
/// Shared entry point for OSC grabs and any future wake source (web, etc.).
/// </summary>
public interface IWakeTrigger
{
    WakeResult RequestWake(string source);
}
