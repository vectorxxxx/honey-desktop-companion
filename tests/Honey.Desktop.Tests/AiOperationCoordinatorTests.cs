using Honey.Desktop.Runtime;

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
}
