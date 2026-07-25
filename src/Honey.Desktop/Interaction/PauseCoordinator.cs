namespace Honey.Desktop.Interaction;

[Flags]
public enum PauseReason
{
    None = 0,
    User = 1,
    Drag = 2
}

public sealed class PauseCoordinator
{
    private readonly Action<bool> _effectivePauseChanged;
    private readonly Action<Exception>? _errorSink;
    private PauseReason _reasons;

    public PauseCoordinator(
        Action<bool> effectivePauseChanged,
        Action<Exception>? errorSink = null)
    {
        _effectivePauseChanged =
            effectivePauseChanged ?? throw new ArgumentNullException(nameof(effectivePauseChanged));
        _errorSink = errorSink;
    }

    public bool EffectivePaused => _reasons != PauseReason.None;

    public bool UserPaused => (_reasons & PauseReason.User) != 0;

    public void Set(PauseReason reason, bool paused)
    {
        if (reason is PauseReason.None || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var before = EffectivePaused;
        _reasons = paused ? _reasons | reason : _reasons & ~reason;
        if (before != EffectivePaused)
        {
            SafeCallback.Invoke(() => _effectivePauseChanged(EffectivePaused), _errorSink);
        }
    }
}
