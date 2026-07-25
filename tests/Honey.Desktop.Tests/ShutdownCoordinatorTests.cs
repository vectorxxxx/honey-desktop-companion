using System.Diagnostics;
using Honey.Desktop.Shutdown;

namespace Honey.Desktop.Tests;

public sealed class ShutdownCoordinatorTests
{
    [Fact]
    public void BlockingBridge_不泵消息的同步上下文中仍可完成异步操作()
    {
        var previous = SynchronizationContext.Current;
        var completed = false;
        var errors = new List<Exception>();
        SynchronizationContext.SetSynchronizationContext(new NonPumpingContext());
        try
        {
            var result = BlockingShutdownBridge.TryRun(
                async () =>
                {
                    await Task.Yield();
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(20),
                        TestContext.Current.CancellationToken);
                    completed = true;
                },
                TimeSpan.FromSeconds(1),
                errors.Add);

            Assert.True(result);
            Assert.True(completed);
            Assert.Empty(errors);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void BlockingBridge_超时会返回且错误可观察()
    {
        var errors = new List<Exception>();
        var stopwatch = Stopwatch.StartNew();

        var result = BlockingShutdownBridge.TryRun(
            () => Task.Delay(TimeSpan.FromSeconds(5)),
            TimeSpan.FromMilliseconds(50),
            errors.Add);

        Assert.False(result);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.IsType<TimeoutException>(Assert.Single(errors));
    }

    [Fact]
    public async Task RequestShutdownAsync_并发请求共享任务且只准备和关闭一次()
    {
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var prepareCount = 0;
        var shutdownCount = 0;
        var coordinator = new ShutdownCoordinator(
            async () =>
            {
                Interlocked.Increment(ref prepareCount);
                await gate.Task;
            },
            () =>
            {
                Interlocked.Increment(ref shutdownCount);
                return Task.CompletedTask;
            });

        var first = coordinator.RequestShutdownAsync();
        var second = coordinator.RequestShutdownAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Same(first, second);
        Assert.Equal(1, prepareCount);
        Assert.Equal(1, shutdownCount);
    }

    [Fact]
    public async Task RequestShutdownAsync_准备失败可观察且仍请求关闭()
    {
        var expected = new InvalidOperationException("最终保存失败");
        var errors = new List<Exception>();
        var shutdownCount = 0;
        var coordinator = new ShutdownCoordinator(
            () => Task.FromException(expected),
            () =>
            {
                shutdownCount++;
                return Task.CompletedTask;
            },
            errors.Add);

        await coordinator.RequestShutdownAsync();

        Assert.Same(expected, Assert.Single(errors));
        Assert.Equal(1, shutdownCount);
    }

    [Fact]
    public async Task AsyncShutdownOperationQueue_停止会等待在途操作并拒绝新操作()
    {
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionCount = 0;
        var queue = new AsyncShutdownOperationQueue();
        Assert.True(queue.TryEnqueue(async () =>
        {
            Interlocked.Increment(ref executionCount);
            started.SetResult();
            await release.Task;
        }));
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var stopTask = queue.StopAsync();

        Assert.False(queue.TryEnqueue(() => Task.CompletedTask));
        Assert.False(stopTask.IsCompleted);
        release.SetResult();
        await stopTask;
        Assert.Equal(1, executionCount);
    }

    private sealed class NonPumpingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }
}
