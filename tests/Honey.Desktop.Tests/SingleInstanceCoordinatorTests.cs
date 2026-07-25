using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

[Collection("单实例进程测试")]
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

    [Fact]
    public async Task SendAsync_向主实例传递Shutdown()
    {
        var handled = new TaskCompletionSource<SingleInstanceCommand>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-tests-{Guid.NewGuid():N}"));
        await using var primary = new SingleInstanceCoordinator(identity);
        primary.StartListening(command =>
        {
            handled.TrySetResult(command);
            return Task.CompletedTask;
        });
        await using var secondary = new SingleInstanceCoordinator(identity);

        Assert.False(secondary.IsPrimary);
        Assert.True(await secondary.SendAsync(
            SingleInstanceCommand.Shutdown,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            SingleInstanceCommand.Shutdown,
            await handled.Task.WaitAsync(
                TimeSpan.FromSeconds(3),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task 不同数据目录拥有彼此隔离的实例身份()
    {
        var firstIdentity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-a-{Guid.NewGuid():N}"));
        var secondIdentity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-b-{Guid.NewGuid():N}"));

        await using var first = new SingleInstanceCoordinator(firstIdentity);
        await using var second = new SingleInstanceCoordinator(secondIdentity);

        Assert.True(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.NotEqual(firstIdentity.PipeName, secondIdentity.PipeName);
    }

    [Fact]
    public async Task SendAsync_接收回执不被慢处理阻塞()
    {
        var handled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-ack-{Guid.NewGuid():N}"));
        await using var primary = new SingleInstanceCoordinator(identity);
        primary.StartListening(async _ =>
        {
            await Task.Delay(150, TestContext.Current.CancellationToken);
            handled.TrySetResult();
        });
        await using var secondary = new SingleInstanceCoordinator(identity);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var sent = await secondary.SendAsync(
            SingleInstanceCommand.Show,
            TestContext.Current.CancellationToken);

        Assert.True(sent);
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(120));
        await handled.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_在主实例慢初始化但稍后监听时重试成功()
    {
        var handled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-slow-{Guid.NewGuid():N}"));
        await using var primary = new SingleInstanceCoordinator(identity);
        await using var secondary = new SingleInstanceCoordinator(identity);

        var send = secondary.SendAsync(
            SingleInstanceCommand.Shutdown,
            TestContext.Current.CancellationToken);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        primary.StartListening(command =>
        {
            Assert.Equal(SingleInstanceCommand.Shutdown, command);
            handled.TrySetResult();
            return Task.CompletedTask;
        });

        Assert.True(await send);
        await handled.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task 立即并发Show与Shutdown均收到回执且释放监听()
    {
        var identity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-pressure-{Guid.NewGuid():N}"));
        var count = 0;
        var primary = new SingleInstanceCoordinator(identity);
        primary.StartListening(_ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });
        var clients = Enumerable.Range(0, 8)
            .Select(_ => new SingleInstanceCoordinator(identity))
            .ToArray();
        try
        {
            var sends = clients.Select((client, index) => client.SendAsync(
                index % 2 == 0
                    ? SingleInstanceCommand.Show
                    : SingleInstanceCommand.Shutdown,
                TestContext.Current.CancellationToken));

            Assert.All(await Task.WhenAll(sends), Assert.True);
            Assert.Equal(8, count);
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
            await primary.DisposeAsync();
        }
    }

    [Fact]
    public async Task 重复启动发送与释放后互斥体和监听均可重新取得()
    {
        var identity = SingleInstanceIdentity.Create(
            Path.Combine(Path.GetTempPath(), $"honey-reuse-{Guid.NewGuid():N}"));
        for (var index = 0; index < 12; index++)
        {
            await using (var primary = new SingleInstanceCoordinator(identity))
            {
                Assert.True(primary.IsPrimary);
                primary.StartListening(_ => Task.CompletedTask);
                await using var secondary = new SingleInstanceCoordinator(identity);
                Assert.True(await secondary.SendAsync(
                    SingleInstanceCommand.Show,
                    TestContext.Current.CancellationToken));
            }
        }
    }

    private static async Task SendShowFromSecondaryAsync()
    {
        await using var secondary = new SingleInstanceCoordinator();
        Assert.False(secondary.IsPrimary);
        Assert.True(await secondary.SendShowAsync(TestContext.Current.CancellationToken));
    }
}
