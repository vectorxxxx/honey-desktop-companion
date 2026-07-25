using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Honey.Desktop.SingleInstance;

public readonly record struct SingleInstanceSendResult(bool Success, int ServerProcessId);

public sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);
    private const int SendAttempts = 4;
    private readonly Mutex _mutex;
    private readonly SingleInstanceIdentity _identity;
    private readonly Action<Exception>? _errorSink;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly object _lifecycleSync = new();
    private readonly ConcurrentDictionary<Guid, Lazy<Task<bool>>> _requests = new();
    private Task? _listenerTask;
    private Task? _disposeTask;
    private bool _disposing;

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
        lock (_lifecycleSync)
        {
            if (_disposing)
            {
                throw new ObjectDisposedException(nameof(SingleInstanceCoordinator));
            }
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
    }

    public Task<bool> SendShowAsync(CancellationToken cancellationToken = default) =>
        SendAsync(SingleInstanceCommand.Show, cancellationToken);

    public Task<bool> SendAsync(
        SingleInstanceCommand command,
        CancellationToken cancellationToken = default) =>
        SendAsync(command, Guid.NewGuid(), cancellationToken);

    public async Task<bool> SendAsync(
        SingleInstanceCommand command,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        (await SendWithResultAsync(command, requestId, cancellationToken).ConfigureAwait(false))
        .Success;

    public Task<SingleInstanceSendResult> SendWithResultAsync(
        SingleInstanceCommand command,
        CancellationToken cancellationToken = default) =>
        SendWithResultAsync(command, Guid.NewGuid(), cancellationToken);

    public async Task<SingleInstanceSendResult> SendWithResultAsync(
        SingleInstanceCommand command,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = new SingleInstanceRequest(requestId, command);
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
                using var writer = new StreamWriter(
                    client,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    leaveOpen: true)
                {
                    AutoFlush = true
                };
                using var reader = new StreamReader(
                    client,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                await writer.WriteLineAsync(
                    SingleInstanceMessage.Serialize(request).AsMemory(),
                    timeoutSource.Token).ConfigureAwait(false);
                var accepted = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
                var acceptedParts = accepted?.Split('|');
                if (acceptedParts is not { Length: 3 } ||
                    !string.Equals(acceptedParts[0], "accepted", StringComparison.Ordinal) ||
                    !string.Equals(acceptedParts[1], requestId.ToString("N"), StringComparison.Ordinal) ||
                    !int.TryParse(acceptedParts[2], out var serverProcessId) ||
                    serverProcessId <= 0)
                {
                    return default;
                }
                var completed = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
                if (string.Equals(
                    completed,
                    $"completed|{requestId:N}|ok",
                    StringComparison.Ordinal))
                {
                    return new SingleInstanceSendResult(true, serverProcessId);
                }
                if (string.Equals(
                    completed,
                    $"completed|{requestId:N}|error",
                    StringComparison.Ordinal))
                {
                    return new SingleInstanceSendResult(false, serverProcessId);
                }
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
            }

            if (attempt + 1 < SendAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(75), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        return default;
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycleSync)
        {
            _disposing = true;
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task ListenAsync(
        Func<SingleInstanceCommand, Task> commandHandler,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _identity.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(
                    server,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                using var writer = new StreamWriter(
                    server,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    leaveOpen: true)
                {
                    AutoFlush = true
                };
                using var readTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readTimeout.CancelAfter(AttemptTimeout);
                var frame = await reader.ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
                if (frame is null ||
                    frame.Length > 96 ||
                    !SingleInstanceMessage.TryParseRequest(frame, out var request))
                {
                    continue;
                }

                await writer.WriteLineAsync(
                    $"accepted|{request.RequestId:N}|{Environment.ProcessId}".AsMemory(),
                    readTimeout.Token).ConfigureAwait(false);
                var result = _requests.GetOrAdd(
                    request.RequestId,
                    _ => new Lazy<Task<bool>>(
                        () => ExecuteCommandAsync(commandHandler, request.Command),
                        LazyThreadSafetyMode.ExecutionAndPublication));
                var success = await result.Value.ConfigureAwait(false);
                await writer.WriteLineAsync(
                    $"completed|{request.RequestId:N}|{(success ? "ok" : "error")}".AsMemory(),
                    readTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (
                exception is OperationCanceledException
                    or IOException
                    or UnauthorizedAccessException)
            {
                await DelayAfterFailure(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> ExecuteCommandAsync(
        Func<SingleInstanceCommand, Task> commandHandler,
        SingleInstanceCommand command)
    {
        try
        {
            await commandHandler(command).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception error) when (!IsProgramFatal(error))
        {
            ObserveError(_errorSink, error);
            return false;
        }
    }

    private static void ObserveError(Action<Exception>? errorSink, Exception error)
    {
        if (errorSink is null) return;
        try { errorSink(error); }
        catch (Exception sinkError) when (!IsProgramFatal(sinkError)) { }
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
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private async Task DisposeCoreAsync()
    {
        await _stopSource.CancelAsync().ConfigureAwait(false);
        Task? listener;
        lock (_lifecycleSync)
        {
            listener = _listenerTask;
        }
        try
        {
            if (listener is not null)
            {
                try { await listener.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            _stopSource.Dispose();
            _mutex.Dispose();
        }
    }
}
