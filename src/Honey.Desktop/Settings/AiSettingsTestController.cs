namespace Honey.Desktop.Settings;

public sealed class AiSettingsTestController
{
    private readonly object _sync = new();
    private CancellationTokenSource? _active;

    public bool IsRunning
    {
        get { lock (_sync) return _active is not null; }
    }

    public async Task<string> RunAsync(
        Func<CancellationToken, Task<string>> test,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(test);
        CancellationTokenSource linked;
        lock (_sync)
        {
            if (_active is not null)
            {
                return "正在测试，请稍候。";
            }

            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _active = linked;
        }

        try
        {
            return await test(linked.Token);
        }
        catch (OperationCanceledException) when (
            linked.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return "测试已取消。";
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_active, linked))
                {
                    _active = null;
                }
            }

            linked.Dispose();
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? active;
        lock (_sync)
        {
            active = _active;
        }

        active?.Cancel();
    }
}
