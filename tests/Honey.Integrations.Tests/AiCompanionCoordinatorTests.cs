using Honey.Integrations.Ai;

namespace Honey.Integrations.Tests;

public sealed class AiCompanionCoordinatorTests
{
    [Fact]
    public async Task RequestAsync_未配置时本地降级且不创建提供器()
    {
        var factoryCalls = 0;
        var coordinator = new AiCompanionCoordinator(() =>
        {
            factoryCalls++;
            return null;
        });

        var result = await coordinator.RequestAsync(
            new AiCompanionRequest("给我灵感", "常态", []),
            _ => throw new InvalidOperationException("不应路由"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Available);
        Assert.Equal("disabled", result.FailureCode);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RequestAsync_单飞拒绝重复点击并仅路由白名单建议()
    {
        var pending = new TaskCompletionSource<AiCompanionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new StubProvider((_, _) => pending.Task);
        var coordinator = new AiCompanionCoordinator(() => provider);
        var routed = new List<string>();
        var first = coordinator.RequestAsync(
            new AiCompanionRequest("给我灵感", "常态", []),
            routed.Add,
            TestContext.Current.CancellationToken);

        var duplicate = await coordinator.RequestAsync(
            new AiCompanionRequest("再来一次", "常态", []),
            routed.Add,
            TestContext.Current.CancellationToken);
        pending.SetResult(new AiCompanionResult(true, "去玩吧。", "play", null));
        var result = await first;

        Assert.Equal("busy", duplicate.FailureCode);
        Assert.True(result.Available);
        Assert.Equal(["play"], routed);
    }

    [Fact]
    public async Task RequestAsync_再次过滤非法建议且路由失败不影响文本()
    {
        var illegal = new AiCompanionCoordinator(() => new StubProvider(
            (_, _) => Task.FromResult(
                new AiCompanionResult(true, "保持安静。", "pounce", null))));

        var illegalResult = await illegal.RequestAsync(
            new AiCompanionRequest("你好", "常态", []),
            _ => throw new InvalidOperationException("不应调用"),
            TestContext.Current.CancellationToken);

        Assert.True(illegalResult.Available);
        Assert.Null(illegalResult.SuggestedIntent);

        var legal = new AiCompanionCoordinator(() => new StubProvider(
            (_, _) => Task.FromResult(
                new AiCompanionResult(true, "去看看。", "observe", null))));
        var legalResult = await legal.RequestAsync(
            new AiCompanionRequest("你好", "常态", []),
            _ => throw new InvalidOperationException("运行时暂不可用"),
            TestContext.Current.CancellationToken);

        Assert.True(legalResult.Available);
        Assert.Equal("去看看。", legalResult.Text);
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
}
