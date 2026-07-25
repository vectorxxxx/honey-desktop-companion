using Honey.Desktop.Runtime;
using Honey.Desktop.Shutdown;
using Honey.Integrations.Ai;
using System.Net;

namespace Honey.Desktop.Tests;

public sealed class AiOperationCoordinatorTests
{
    [Fact]
    public async Task StopAsync_取消并等待所有在途操作且观察异常()
    {
        var cancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new AiOperationCoordinator();
        _ = coordinator.RunAsync(async token =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                cancelled.SetResult();
            }
        });

        await coordinator.StopAsync();

        await cancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(coordinator.IsStopped);
    }

    [Fact]
    public void 阻塞退出桥在单线程上下文中可取消并排空网络请求()
    {
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new NeverRunsSynchronizationContext());
            using var client = new HttpClient(new StubHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));
            var provider = new OpenAiCompatibleProvider(
                client,
                new AiOptions(
                    "https://example.com/v1",
                    "model",
                    "sk-secret",
                    TimeSpan.FromSeconds(15)));
            var coordinator = new AiOperationCoordinator();
            _ = coordinator.RunAsync(token => CompleteAsync(provider, token));

            var completed = BlockingShutdownBridge.TryRun(
                coordinator.StopAsync,
                TimeSpan.FromSeconds(1));

            Assert.True(completed);
            Assert.True(coordinator.IsStopped);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task CompleteAsync(
        IAiCompanionProvider provider,
        CancellationToken cancellationToken)
    {
        await provider.CompleteAsync(
                new AiCompanionRequest("你好", "常态", []),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class NeverRunsSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
