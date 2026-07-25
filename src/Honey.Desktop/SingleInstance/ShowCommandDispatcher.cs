namespace Honey.Desktop.SingleInstance;

public sealed class ShowCommandDispatcher
{
    private readonly Func<bool> _isShuttingDown;
    private readonly Action<Action> _post;
    private readonly Action _restoreWindow;

    public ShowCommandDispatcher(
        Func<bool> isShuttingDown,
        Action<Action> post,
        Action restoreWindow)
    {
        _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _restoreWindow = restoreWindow ?? throw new ArgumentNullException(nameof(restoreWindow));
    }

    public Task Handle()
    {
        if (_isShuttingDown())
        {
            return Task.CompletedTask;
        }

        try
        {
            _post(() =>
            {
                if (!_isShuttingDown())
                {
                    _restoreWindow();
                }
            });
        }
        catch (InvalidOperationException) when (_isShuttingDown())
        {
            // WPF 调度器关停与命令投递可能并发发生，此时无需恢复窗口。
        }

        return Task.CompletedTask;
    }
}
