namespace VRCWakeMe.Core;

public readonly record struct OscMessage(string Address, IReadOnlyList<object?> Arguments)
{
    public object? FirstArgument => Arguments.Count > 0 ? Arguments[0] : null;
}
