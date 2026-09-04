namespace VRCWakeMe.Core;

public static class OscValue
{
    public static bool IsOn(object? value) => value switch
    {
        null => false,
        bool b => b,
        byte by => by != 0,
        sbyte sb => sb != 0,
        short s => s != 0,
        ushort us => us != 0,
        int i => i != 0,
        uint ui => ui != 0,
        long l => l != 0,
        ulong ul => ul != 0,
        float f => f >= 0.5f,
        double d => d >= 0.5,
        decimal m => m >= 0.5m,
        string s => s is "1" or "true" or "True" or "TRUE",
        _ => false
    };
}
