using Honey.Integrations.Ai;

namespace Honey.Integrations.Tests;

public sealed class AiRequestGateTests
{
    [Fact]
    public async Task Coordinator_成功完成后十秒冷却且边界使用单调时间()
    {
        var time = new ManualTimeProvider();
        var gate = new AiRequestGate(time, TimeSpan.FromSeconds(10));
        var calls = 0;
        var provider = new StubProvider((_, _) =>
        {
            calls++;
            return Task.FromResult(new AiCompanionResult(true, "好。", null, null));
        });
        var coordinator = new AiCompanionCoordinator(() => provider, gate);
        var request = new AiCompanionRequest("你好", "常态", []);
        var token = TestContext.Current.CancellationToken;

        Assert.True((await coordinator.RequestAsync(request, _ => { }, token)).Available);
        Assert.Equal("cooldown", (await coordinator.RequestAsync(request, _ => { }, token)).FailureCode);
        time.Advance(TimeSpan.FromMilliseconds(9_999));
        Assert.Equal("cooldown", (await coordinator.RequestAsync(request, _ => { }, token)).FailureCode);
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True((await coordinator.RequestAsync(request, _ => { }, token)).Available);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Coordinator_失败和调用方取消后也进入冷却()
    {
        var time = new ManualTimeProvider();
        var failedGate = new AiRequestGate(time, TimeSpan.FromSeconds(10));
        var failed = new AiCompanionCoordinator(
            () => new StubProvider((_, _) => Task.FromResult(
                new AiCompanionResult(false, null, null, "network"))),
            failedGate);
        var request = new AiCompanionRequest("你好", "常态", []);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal("network", (await failed.RequestAsync(request, _ => { }, token)).FailureCode);
        Assert.Equal("cooldown", (await failed.RequestAsync(request, _ => { }, token)).FailureCode);

        var cancelGate = new AiRequestGate(time, TimeSpan.FromSeconds(10));
        var cancel = new AiCompanionCoordinator(
            () => new StubProvider(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("不可到达");
            }),
            cancelGate);
        using var source = new CancellationTokenSource();
        var pending = cancel.RequestAsync(request, _ => { }, source.Token);
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(
            "cooldown",
            (await cancel.RequestAsync(request, _ => { }, TestContext.Current.CancellationToken)).FailureCode);
    }

    [Fact]
    public async Task SharedGate_跨两个调用入口保持单飞和冷却()
    {
        var gate = new AiRequestGate(TimeProvider.System, TimeSpan.FromSeconds(10));
        var pending = new TaskCompletionSource<AiCompanionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProvider = new StubProvider((_, _) => pending.Task);
        var secondCalls = 0;
        var first = new AiCompanionCoordinator(() => firstProvider, gate);
        var secondProvider = new StubProvider((_, _) =>
        {
            secondCalls++;
            return Task.FromResult(new AiCompanionResult(true, "好。", null, null));
        });
        var settingsTester = new AiConnectionTester(gate);
        var request = new AiCompanionRequest("你好", "常态", []);

        var active = first.RequestAsync(request, _ => { }, TestContext.Current.CancellationToken);
        Assert.Equal(
            "busy",
            (await settingsTester.TestAsync(
                secondProvider,
                request,
                TestContext.Current.CancellationToken)).FailureCode);
        pending.SetResult(new AiCompanionResult(true, "完成。", null, null));
        await active;
        Assert.Equal(
            "cooldown",
            (await settingsTester.TestAsync(
                secondProvider,
                request,
                TestContext.Current.CancellationToken)).FailureCode);
        Assert.Equal(0, secondCalls);

        var reverseGate = new AiRequestGate(TimeProvider.System, TimeSpan.FromSeconds(10));
        var reversePending = new TaskCompletionSource<AiCompanionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reverseSettings = new AiConnectionTester(reverseGate);
        var settingsActive = reverseSettings.TestAsync(
            new StubProvider((_, _) => reversePending.Task),
            request,
            TestContext.Current.CancellationToken);
        var ring = new AiCompanionCoordinator(() => secondProvider, reverseGate);
        Assert.Equal(
            "busy",
            (await ring.RequestAsync(
                request,
                _ => { },
                TestContext.Current.CancellationToken)).FailureCode);
        reversePending.SetResult(new AiCompanionResult(true, "完成。", null, null));
        await settingsActive;
        Assert.Equal(0, secondCalls);
    }

    private sealed class StubProvider(
        Func<AiCompanionRequest, CancellationToken, Task<AiCompanionResult>> complete)
        : IAiCompanionProvider
    {
        public Task<AiCompanionResult> CompleteAsync(
            AiCompanionRequest request,
            CancellationToken cancellationToken) =>
            complete(request, cancellationToken);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
