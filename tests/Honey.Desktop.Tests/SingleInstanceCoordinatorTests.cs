using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task 处理器首次失败后报告错误并继续处理下一条Show()
    {
        var observedError = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondHandled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var primary = new SingleInstanceCoordinator(
            errorSink: error => observedError.TrySetResult(error));
        primary.StartListening(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("首条命令故障");
            }

            secondHandled.TrySetResult();
            return Task.CompletedTask;
        });

        await SendShowFromSecondaryAsync();
        var error = await observedError.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        await SendShowFromSecondaryAsync();
        await secondHandled.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        await primary.DisposeAsync();

        Assert.Equal("首条命令故障", error.Message);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task 错误观察器自身抛出时不会击穿监听()
    {
        var firstAttempted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondHandled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var primary = new SingleInstanceCoordinator(
            errorSink: _ => throw new InvalidOperationException("观察器故障"));
        primary.StartListening(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstAttempted.TrySetResult();
                throw new InvalidOperationException("命令故障");
            }

            secondHandled.TrySetResult();
            return Task.CompletedTask;
        });

        await SendShowFromSecondaryAsync();
        await firstAttempted.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        await SendShowFromSecondaryAsync();
        await secondHandled.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        await primary.DisposeAsync();

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task 处理器失败后的退避可被Dispose立即取消()
    {
        var observedError = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var primary = new SingleInstanceCoordinator(
            errorSink: _ => observedError.TrySetResult());
        primary.StartListening(_ => throw new InvalidOperationException("命令故障"));

        await SendShowFromSecondaryAsync();
        await observedError.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await primary.DisposeAsync();
        watch.Stop();

        Assert.True(
            watch.Elapsed < TimeSpan.FromMilliseconds(500),
            $"关停耗时不应包含完整退避，实际为 {watch.Elapsed.TotalMilliseconds:F0}ms。");
    }

    [Fact]
    public async Task DisposeAsync_重复调用安全且共享同一次关停()
    {
        var coordinator = new SingleInstanceCoordinator();

        await coordinator.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    private static async Task SendShowFromSecondaryAsync()
    {
        await using var secondary = new SingleInstanceCoordinator();
        Assert.False(secondary.IsPrimary);
        Assert.True(await secondary.SendShowAsync(TestContext.Current.CancellationToken));
    }
}
