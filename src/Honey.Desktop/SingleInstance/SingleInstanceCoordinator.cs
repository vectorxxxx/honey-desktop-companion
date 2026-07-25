using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Honey.Desktop.SingleInstance;

public sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    public const string MutexName = @"Local\Honey.Desktop.Singleton";
    private const string PipeName = "Honey.Desktop.Commands";
    private const int MaximumMessageBytes = 32;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan MessageTimeout = TimeSpan.FromMilliseconds(1000);

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly object _disposeSync = new();
    private Task? _listenerTask;
    private Task? _disposeTask;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public void StartListening(Func<SingleInstanceCommand, Task> commandHandler)
    {
        ArgumentNullException.ThrowIfNull(commandHandler);
        if (!IsPrimary)
        {
            throw new InvalidOperationException("只有主实例可以监听命令。");
        }

        if (_listenerTask is not null)
        {
            throw new InvalidOperationException("命令监听已经启动。");
        }

        _listenerTask = ListenAsync(commandHandler, _stopSource.Token);
    }

    public async Task<bool> SendShowAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ConnectionTimeout);
        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            await client.WriteAsync(Encoding.UTF8.GetBytes("show"), timeoutSource.Token)
                .ConfigureAwait(false);
            await client.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private static async Task ListenAsync(
        Func<SingleInstanceCommand, Task> commandHandler,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    MaximumMessageBytes,
                    MaximumMessageBytes);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var messageTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                messageTimeout.CancelAfter(MessageTimeout);
                var buffer = new byte[MaximumMessageBytes + 1];
                var bytesRead = 0;
                while (bytesRead < buffer.Length)
                {
                    var count = await server
                        .ReadAsync(buffer.AsMemory(bytesRead), messageTimeout.Token)
                        .ConfigureAwait(false);
                    if (count == 0)
                    {
                        break;
                    }

                    bytesRead += count;
                }

                if (SingleInstanceMessage.TryParse(buffer.AsSpan(0, bytesRead), out var command))
                {
                    await commandHandler(command).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                await DelayAfterFailure(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                await DelayAfterFailure(cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await DelayAfterFailure(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DelayAfterFailure(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _stopSource.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_listenerTask is not null)
            {
                try
                {
                    await _listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            _stopSource.Dispose();
            _mutex.Dispose();
        }
    }
}
