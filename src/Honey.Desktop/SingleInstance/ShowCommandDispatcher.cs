namespace Honey.Desktop.SingleInstance;

public sealed class ShowCommandDispatcher
{
    private readonly Func<bool> _isShuttingDown;
    private readonly Func<Action, Task> _post;
    private readonly Action _restoreWindow;

    public ShowCommandDispatcher(
        Func<bool> isShuttingDown,
        Func<Action, Task> post,
        Action restoreWindow)
    {
        _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _restoreWindow = restoreWindow ?? throw new ArgumentNullException(nameof(restoreWindow));
    }

    public async Task Handle()
    {
        if (_isShuttingDown())
        {
            return;
        }

        try
        {
            await _post(() =>
            {
                if (!_isShuttingDown())
                {
                    _restoreWindow();
                }
            }).ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (_isShuttingDown())
        {
            // WPF 调度器关停与命令投递可能并发发生，此时无需恢复窗口。
        }

    }
}
