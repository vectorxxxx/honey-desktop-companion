namespace Honey.Desktop.Settings;

public sealed class AiSettingsTestController
{
    private readonly object _sync = new();
    private ActiveRequest? _active;

    public bool IsRunning
    {
        get { lock (_sync) return _active is not null; }
    }

    public async Task<string> RunAsync(
        Func<CancellationToken, Task<string>> test,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(test);
        ActiveRequest active;
        lock (_sync)
        {
            if (_active is not null)
            {
                return "正在测试，请稍候。";
            }

            active = new ActiveRequest(
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            _active = active;
        }

        try
        {
            return await test(active.Source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            active.Source.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return "测试已取消。";
        }
        finally
        {
            lock (_sync)
            {
                active.Completed = true;
                if (ReferenceEquals(_active, active) && !active.CancelInProgress)
                {
                    _active = null;
                    active.Source.Dispose();
                }
            }
        }
    }

    public void Cancel()
    {
        ActiveRequest? active;
        lock (_sync)
        {
            active = _active;
            if (active is null || active.CancelInProgress)
            {
                return;
            }

            active.CancelInProgress = true;
        }

        try
        {
            active.Source.Cancel();
        }
        finally
        {
            lock (_sync)
            {
                active.CancelInProgress = false;
                if (ReferenceEquals(_active, active) && active.Completed)
                {
                    _active = null;
                    active.Source.Dispose();
                }
            }
        }
    }

    private sealed class ActiveRequest(CancellationTokenSource source)
    {
        public CancellationTokenSource Source { get; } = source;
        public bool CancelInProgress { get; set; }
        public bool Completed { get; set; }
    }
}
