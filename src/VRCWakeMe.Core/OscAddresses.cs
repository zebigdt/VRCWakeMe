namespace VRCWakeMe.Core;

public static class OscAddresses
{
    public const string Grabbed = "/avatar/parameters/grabbed_IsGrabbed";

    public static bool IsGrabbed(string address) =>
        string.Equals(address, Grabbed, StringComparison.Ordinal);
}
