namespace VRCWakeMe.Core;

public sealed class OscTouchedTracker
{
    private readonly RisingEdgeDetector _edge = new();

    public bool Observe(string address, object? value)
    {
        if (!OscAddresses.IsTouched(address))
        {
            return false;
        }

        return _edge.Observe(OscValue.IsOn(value));
    }

    public void Reset() => _edge.Reset();
}
