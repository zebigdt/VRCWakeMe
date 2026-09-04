namespace VRCWakeMe.Core;

public sealed class WakeCoordinator : IWakeTrigger
{
    private readonly object _gate = new();
    private bool _armed = true;
    private bool _playing;
    private DateTimeOffset? _lastStartUtc;
    private DateTimeOffset? _stopAtUtc;

    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromSeconds(45);

    public event Action? AlarmStarted;
    public event Action? AlarmStopped;
    public event Action? StateChanged;

    public bool Armed
    {
        get
        {
            lock (_gate)
            {
                return _armed;
            }
        }
        set
        {
            var stopped = false;
            lock (_gate)
            {
                if (_armed == value)
                {
                    return;
                }

                _armed = value;
                if (!value && _playing)
                {
                    _playing = false;
                    _stopAtUtc = null;
                    stopped = true;
                }
            }

            if (stopped)
            {
                AlarmStopped?.Invoke();
            }

            StateChanged?.Invoke();
        }
    }

    public bool IsPlaying
    {
        get
        {
            lock (_gate)
            {
                return _playing;
            }
        }
    }

    public WakeResult RequestWake(string source) => RequestWake(source, DateTimeOffset.UtcNow);

    public WakeResult RequestWake(string source, DateTimeOffset nowUtc)
    {
        _ = source;
        WakeResult result;
        var started = false;

        lock (_gate)
        {
            if (!_armed)
            {
                result = WakeResult.Disarmed;
            }
            else if (_playing)
            {
                result = WakeResult.AlreadyPlaying;
            }
            else if (_lastStartUtc is { } last && nowUtc - last < Cooldown)
            {
                result = WakeResult.OnCooldown;
            }
            else
            {
                _playing = true;
                _lastStartUtc = nowUtc;
                _stopAtUtc = nowUtc + MaxDuration;
                result = WakeResult.Started;
                started = true;
            }
        }

        if (started)
        {
            AlarmStarted?.Invoke();
            StateChanged?.Invoke();
        }

        return result;
    }

    public void Dismiss()
    {
        var stopped = false;
        lock (_gate)
        {
            if (!_playing)
            {
                return;
            }

            _playing = false;
            _stopAtUtc = null;
            stopped = true;
        }

        if (stopped)
        {
            AlarmStopped?.Invoke();
            StateChanged?.Invoke();
        }
    }

    public void Tick(DateTimeOffset nowUtc)
    {
        var stopped = false;
        lock (_gate)
        {
            if (_playing && _stopAtUtc is { } stopAt && nowUtc >= stopAt)
            {
                _playing = false;
                _stopAtUtc = null;
                stopped = true;
            }
        }

        if (stopped)
        {
            AlarmStopped?.Invoke();
            StateChanged?.Invoke();
        }
    }

    public void Tick() => Tick(DateTimeOffset.UtcNow);
}
