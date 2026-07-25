using System.Diagnostics;

namespace Honey.Desktop.Runtime;

public sealed class AiOperationCoordinator
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Task> _operations = [];
    private bool _stopped;

    public bool IsStopped
    {
        get { lock (_sync) return _stopped; }
    }

    public Task RunAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_sync)
        {
            if (_stopped)
            {
                return Task.CompletedTask;
            }

            var task = ObserveAsync(operation, _lifetime.Token);
            _operations.Add(task);
            _ = task.ContinueWith(
                completed =>
                {
                    lock (_sync)
                    {
                        _operations.Remove(completed);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }
    }

    public Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_sync)
        {
            if (_stopped)
            {
                return Task.FromCanceled<T>(new CancellationToken(canceled: true));
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetime.Token,
                cancellationToken);
            var task = RunResultAsync(operation, linked);
            _operations.Add(task);
            _ = task.ContinueWith(
                completed =>
                {
                    lock (_sync)
                    {
                        _operations.Remove(completed);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }
    }

    public async Task StopAsync()
    {
        Task[] pending;
        lock (_sync)
        {
            if (_stopped)
            {
                pending = [.. _operations];
            }
            else
            {
                _stopped = true;
                _lifetime.Cancel();
                pending = [.. _operations];
            }
        }

        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError("退出时已观察 AI 操作异常：{0}", exception);
        }
    }

    private static async Task ObserveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError("AI 操作失败并已安全观察：{0}", exception);
        }
    }

    private static async Task<T> RunResultAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationTokenSource linked)
    {
        using (linked)
        {
            return await operation(linked.Token).ConfigureAwait(false);
        }
    }
}
