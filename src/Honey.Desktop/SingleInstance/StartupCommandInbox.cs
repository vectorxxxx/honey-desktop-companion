namespace Honey.Desktop.SingleInstance;

public sealed class StartupCommandInbox
{
    private readonly Action _cancelStartup;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Func<SingleInstanceCommand, Task>? _readyHandler;
    private bool _showPending;
    private bool _shutdownPending;

    public StartupCommandInbox(Action cancelStartup)
    {
        _cancelStartup = cancelStartup ?? throw new ArgumentNullException(nameof(cancelStartup));
    }

    public async Task HandleAsync(SingleInstanceCommand command)
    {
        Func<SingleInstanceCommand, Task>? handler;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (command == SingleInstanceCommand.Shutdown)
            {
                if (!_shutdownPending)
                {
                    _shutdownPending = true;
                    _showPending = false;
                    if (_readyHandler is null)
                    {
                        _cancelStartup();
                    }
                }
            }
            else if (!_shutdownPending)
            {
                _showPending = true;
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
            await readyHandler(command).ConfigureAwait(false);
        }
    }
}
