using System.Diagnostics;

namespace Honey.Desktop.Shutdown;

public sealed class ShutdownCoordinator
{
    private readonly object _sync = new();
    private readonly Func<Task> _prepare;
    private readonly Func<Task> _requestApplicationShutdown;
    private readonly Action<Exception>? _errorSink;
    private Task? _preparationTask;
    private Task? _requestTask;

    public ShutdownCoordinator(
        Func<Task> prepare,
        Func<Task> requestApplicationShutdown,
        Action<Exception>? errorSink = null)
    {
        _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        _requestApplicationShutdown = requestApplicationShutdown
            ?? throw new ArgumentNullException(nameof(requestApplicationShutdown));
        _errorSink = errorSink;
    }

    public Task PrepareAsync()
    {
        lock (_sync)
        {
            return _preparationTask ??= Task.Run(_prepare);
        }
    }

    public Task RequestShutdownAsync()
    {
        lock (_sync)
        {
            return _requestTask ??= RequestCoreAsync();
        }
    }

    public bool IsPreparationCompleted
    {
        get
        {
            lock (_sync)
            {
                return _preparationTask?.IsCompleted == true;
            }
        }
    }

    private async Task RequestCoreAsync()
    {
        try
        {
            await PrepareAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (!ShutdownExceptionPolicy.IsFatal(exception))
        {
            ShutdownExceptionPolicy.Observe(_errorSink, exception);
        }

        try
        {
            await _requestApplicationShutdown().ConfigureAwait(false);
        }
        catch (Exception exception) when (!ShutdownExceptionPolicy.IsFatal(exception))
        {
            ShutdownExceptionPolicy.Observe(_errorSink, exception);
        }
    }
}

public static class BlockingShutdownBridge
{
    public static bool TryRun(
        Func<Task> operation,
        TimeSpan timeout,
        Action<Exception>? errorSink = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        Task operationTask;
        try
        {
            operationTask = Task.Run(operation);
        }
        catch (Exception exception) when (!ShutdownExceptionPolicy.IsFatal(exception))
        {
            ShutdownExceptionPolicy.Observe(errorSink, exception);
            return false;
        }

        try
        {
            if (operationTask.Wait(timeout))
            {
                return true;
            }

            ShutdownExceptionPolicy.Observe(
                errorSink,
                new TimeoutException($"退出准备超过时限 {timeout}。"));
            _ = operationTask.ContinueWith(
                task => ShutdownExceptionPolicy.Observe(
                    errorSink,
                    task.Exception?.GetBaseException()
                        ?? new InvalidOperationException("退出准备失败。")),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted
                    | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return false;
        }
        catch (AggregateException exception)
        {
            ShutdownExceptionPolicy.Observe(
                errorSink,
                exception.InnerExceptions.Count == 1
                    ? exception.InnerExceptions[0]
                    : exception.Flatten());
            return false;
        }
    }
}

public sealed class AsyncShutdownOperationQueue
{
    private readonly object _sync = new();
    private Task _tail = Task.CompletedTask;
    private bool _stopped;

    public bool TryEnqueue(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_sync)
        {
            if (_stopped)
            {
                return false;
            }

            _tail = RunAfterAsync(_tail, operation);
            return true;
        }
    }

    public Task StopAsync()
    {
        lock (_sync)
        {
            _stopped = true;
            return _tail;
        }
    }

    private static async Task RunAfterAsync(
        Task predecessor,
        Func<Task> operation)
    {
        await predecessor.ConfigureAwait(false);
        await operation().ConfigureAwait(false);
    }
}

internal static class ShutdownExceptionPolicy
{
    public static void Observe(Action<Exception>? errorSink, Exception exception)
    {
        Trace.TraceError("应用退出准备失败：{0}", exception);
        if (errorSink is null)
        {
            return;
        }

        try
        {
            errorSink(exception);
        }
        catch (Exception sinkException) when (!IsFatal(sinkException))
        {
            Trace.TraceError("应用退出错误观察者失败：{0}", sinkException);
        }
    }

    public static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException;
}
