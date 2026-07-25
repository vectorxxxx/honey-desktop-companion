namespace Honey.Integrations.Ai;

public sealed class AiRequestGate
{
    public static TimeSpan DefaultCooldown { get; } = TimeSpan.FromSeconds(10);

    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cooldown;
    private bool _inFlight;
    private long? _lastCompletionTimestamp;

    public AiRequestGate(
        TimeProvider? timeProvider = null,
        TimeSpan? cooldown = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _cooldown = cooldown ?? DefaultCooldown;
        if (_cooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldown), "AI 冷却时间不得为负数。");
        }
    }

    public bool TryAcquire(out AiRequestLease? lease, out string? failureCode)
    {
        lock (_sync)
        {
            if (_inFlight)
            {
                lease = null;
                failureCode = "busy";
                return false;
            }

            if (_lastCompletionTimestamp is { } completed
                && _timeProvider.GetElapsedTime(completed, _timeProvider.GetTimestamp()) < _cooldown)
            {
                lease = null;
                failureCode = "cooldown";
                return false;
            }

            _inFlight = true;
            lease = new AiRequestLease(Release);
            failureCode = null;
            return true;
        }
    }

    private void Release()
    {
        lock (_sync)
        {
            _lastCompletionTimestamp = _timeProvider.GetTimestamp();
            _inFlight = false;
        }
    }
}

public sealed class AiRequestLease(Action release) : IDisposable
{
    private Action? _release = release;

    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
