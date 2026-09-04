namespace VRCWakeMe.Core;

public static class OscAddresses
{
    public const string Touched = "/avatar/parameters/WakeMe/Touched";

    public static bool IsTouched(string address) =>
        string.Equals(address, Touched, StringComparison.Ordinal);
}
