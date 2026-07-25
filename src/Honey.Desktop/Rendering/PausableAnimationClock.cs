namespace Honey.Desktop.Rendering;

[Flags]
public enum AnimationPauseReason
{
    None = 0,
    User = 1,
    Hidden = 2
}

public sealed class PausableAnimationClock
{
    private readonly Func<TimeSpan> _now;
    private readonly TimeSpan _startedAt;
    private AnimationPauseReason _reasons;
    private TimeSpan _pauseStartedAt;
    private TimeSpan _totalPaused;

    public PausableAnimationClock(Func<TimeSpan>? now = null)
    {
        _now = now ?? (() => TimeSpan.FromSeconds(System.Diagnostics.Stopwatch.GetTimestamp()
            / (double)System.Diagnostics.Stopwatch.Frequency));
        _startedAt = _now();
    }

    public TimeSpan Elapsed
    {
        get
        {
            var current = _now();
            var activePause = _reasons == AnimationPauseReason.None
                ? TimeSpan.Zero
                : current - _pauseStartedAt;
            return current - _startedAt - _totalPaused - activePause;
        }
    }

    public void SetPaused(AnimationPauseReason reason, bool paused)
    {
        if (reason is AnimationPauseReason.None || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var wasPaused = _reasons != AnimationPauseReason.None;
        _reasons = paused ? _reasons | reason : _reasons & ~reason;
        var isPaused = _reasons != AnimationPauseReason.None;
        if (!wasPaused && isPaused)
        {
            _pauseStartedAt = _now();
        }
        else if (wasPaused && !isPaused)
        {
            _totalPaused += _now() - _pauseStartedAt;
        }
    }
}
