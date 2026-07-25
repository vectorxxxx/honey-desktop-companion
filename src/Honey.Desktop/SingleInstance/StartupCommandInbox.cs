namespace Honey.Desktop.SingleInstance;

public sealed class StartupCommandInbox
{
    private readonly Action _cancelStartup;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Func<SingleInstanceCommand, Task>? _readyHandler;
    private bool _showPending;
    private bool _shutdownPending;
    private readonly List<TaskCompletionSource> _pendingShowCompletions = [];

    public StartupCommandInbox(Action cancelStartup)
    {
        _cancelStartup = cancelStartup ?? throw new ArgumentNullException(nameof(cancelStartup));
    }

    public async Task HandleAsync(SingleInstanceCommand command)
    {
        Func<SingleInstanceCommand, Task>? handler;
        TaskCompletionSource? pendingCompletion = null;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (command == SingleInstanceCommand.Shutdown)
            {
                if (!_shutdownPending)
                {
                    _shutdownPending = true;
                    _showPending = false;
                    foreach (var completion in _pendingShowCompletions)
                    {
                        completion.TrySetCanceled();
                    }
                    _pendingShowCompletions.Clear();
                    if (_readyHandler is null)
                    {
                        _cancelStartup();
                    }
                }
            }
            else if (!_shutdownPending)
            {
                _showPending = true;
                if (_readyHandler is null)
                {
                    pendingCompletion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _pendingShowCompletions.Add(pendingCompletion);
                }
            }

            handler = _readyHandler;
            if (command == SingleInstanceCommand.Show && _shutdownPending)
            {
                handler = null;
            }
        }
        finally
        {
            _gate.Release();
        }

        if (handler is not null)
        {
            await handler(command).ConfigureAwait(false);
        }
        else if (pendingCompletion is not null)
        {
            await pendingCompletion.Task.ConfigureAwait(false);
        }
    }

    public async Task AttachAsync(Func<SingleInstanceCommand, Task> readyHandler)
    {
        ArgumentNullException.ThrowIfNull(readyHandler);
        SingleInstanceCommand? pending = null;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_readyHandler is not null)
            {
                throw new InvalidOperationException("启动命令收件箱已经就绪。");
            }

            _readyHandler = readyHandler;
            if (_shutdownPending)
            {
                pending = SingleInstanceCommand.Shutdown;
            }
            else if (_showPending)
            {
                pending = SingleInstanceCommand.Show;
            }
            _showPending = false;
        }
        finally
        {
            _gate.Release();
        }

        if (pending is { } command)
        {
            try
            {
                await readyHandler(command).ConfigureAwait(false);
                foreach (var completion in _pendingShowCompletions)
                {
                    completion.TrySetResult();
                }
            }
            catch (Exception exception)
            {
                foreach (var completion in _pendingShowCompletions)
                {
                    completion.TrySetException(exception);
                }
                throw;
            }
            finally
            {
                _pendingShowCompletions.Clear();
            }
        }
    }
}
