namespace VRCWakeMe.Core;

/// <summary>
/// Fires only on false → true. A grab that stays held will not retrigger.
/// </summary>
public sealed class RisingEdgeDetector
{
    private bool _previous;

    public bool Observe(bool current)
    {
        var rising = current && !_previous;
        _previous = current;
        return rising;
    }

    public void Reset() => _previous = false;
}
