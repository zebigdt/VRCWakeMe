namespace VRCWakeMe.Core;

/// <summary>
/// Shared entry point for OSC pokes and any future wake source (web, etc.).
/// </summary>
public interface IWakeTrigger
{
    WakeResult RequestWake(string source);
}
