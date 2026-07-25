using System.IO;
using System.IO.Pipes;

namespace Honey.Desktop.SingleInstance;

public sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    private const int MaximumMessageBytes = 32;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(1250);
    private const int SendAttempts = 3;
    private static readonly byte[] Acknowledgement = "ok\n"u8.ToArray();

    private readonly Mutex _mutex;
    private readonly SingleInstanceIdentity _identity;
    private readonly Action<Exception>? _errorSink;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly object _disposeSync = new();
    private Task? _listenerTask;
    private Task? _disposeTask;

    public SingleInstanceCoordinator(Action<Exception>? errorSink = null)
        : this(SingleInstanceIdentity.Default, errorSink)
    {
    }

    public SingleInstanceCoordinator(
        SingleInstanceIdentity identity,
        Action<Exception>? errorSink = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _errorSink = errorSink;
        _mutex = new Mutex(initiallyOwned: false, _identity.MutexName, out var createdNew);
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

        _listenerTask = ListenAsync(
            _identity.PipeName,
            commandHandler,
            _errorSink,
            _stopSource.Token);
    }

    public Task<bool> SendShowAsync(CancellationToken cancellationToken = default) =>
        SendAsync(SingleInstanceCommand.Show, cancellationToken);

    public async Task<bool> SendAsync(
        SingleInstanceCommand command,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < SendAttempts; attempt++)
        {
            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(AttemptTimeout);
            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    _identity.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
                await client.WriteAsync(
                        SingleInstanceMessage.Serialize(command),
                        timeoutSource.Token)
                    .ConfigureAwait(false);
                await client.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
                var response = new byte[Acknowledgement.Length];
                await client.ReadExactlyAsync(response, timeoutSource.Token).ConfigureAwait(false);
                return response.AsSpan().SequenceEqual(Acknowledgement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is OperationCanceledException
                    or IOException
                    or UnauthorizedAccessException)
            {
                if (attempt + 1 < SendAttempts)
                {
                    await Task.Delay(
                            TimeSpan.FromMilliseconds(50),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        return false;
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
        string pipeName,
        Func<SingleInstanceCommand, Task> commandHandler,
        Action<Exception>? errorSink,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    MaximumMessageBytes,
                    MaximumMessageBytes);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var messageTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                messageTimeout.CancelAfter(AttemptTimeout);
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
                    if (buffer.AsSpan(0, bytesRead).Contains((byte)'\n'))
                    {
                        break;
                    }
                }

                var newline = buffer.AsSpan(0, bytesRead).IndexOf((byte)'\n');
                if (newline >= 0
                    && SingleInstanceMessage.TryParse(buffer.AsSpan(0, newline), out var command))
                {
                    await server.WriteAsync(Acknowledgement, messageTimeout.Token)
                        .ConfigureAwait(false);
                    await server.FlushAsync(messageTimeout.Token).ConfigureAwait(false);
                    var shouldContinue = await HandleCommandAsync(
                            commandHandler,
                            command,
                            errorSink,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!shouldContinue)
                    {
                        break;
                    }
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

    private static async Task<bool> HandleCommandAsync(
        Func<SingleInstanceCommand, Task> commandHandler,
        SingleInstanceCommand command,
        Action<Exception>? errorSink,
        CancellationToken cancellationToken)
    {
        try
        {
            await commandHandler(command).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception error) when (!IsProgramFatal(error))
        {
            ObserveError(errorSink, error);
            await DelayAfterFailure(cancellationToken).ConfigureAwait(false);
            return !cancellationToken.IsCancellationRequested;
        }
    }

    private static void ObserveError(Action<Exception>? errorSink, Exception error)
    {
        if (errorSink is null)
        {
            return;
        }

        try
        {
            errorSink(error);
        }
        catch (Exception sinkError) when (!IsProgramFatal(sinkError))
        {
            // 错误观察入口不能反向击穿单实例监听。
        }
    }

    private static bool IsProgramFatal(Exception error) =>
        error is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException;

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
