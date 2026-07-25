using Honey.Desktop.Settings;

namespace Honey.Desktop.Tests;

public sealed class AiSettingsTestControllerTests
{
    [Fact]
    public async Task RunAsync_快速双击只启动一次并返回忙碌()
    {
        var calls = 0;
        var pending = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new AiSettingsTestController();

        var first = controller.RunAsync(_ =>
        {
            calls++;
            return pending.Task;
        }, TestContext.Current.CancellationToken);
        var duplicate = await controller.RunAsync(
            _ => throw new InvalidOperationException("不应开始第二次请求"),
            TestContext.Current.CancellationToken);
        pending.SetResult("连接成功");

        Assert.Equal("正在测试，请稍候。", duplicate);
        Assert.Equal("连接成功", await first);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancel_窗口关闭时取消在途请求()
    {
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new AiSettingsTestController();
        var pending = controller.RunAsync(async token =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return "不可到达";
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.SetResult();
                throw;
            }
        }, TestContext.Current.CancellationToken);

        controller.Cancel();

        Assert.Equal("测试已取消。", await pending);
        await cancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cancel_与请求完成并发时幂等且无遗留()
    {
        for (var index = 0; index < 200; index++)
        {
            var controller = new AiSettingsTestController();
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = controller.RunAsync(_ => completion.Task, TestContext.Current.CancellationToken);

            await Task.WhenAll(
                Task.Run(controller.Cancel, TestContext.Current.CancellationToken),
                Task.Run(
                    () => completion.TrySetResult("完成"),
                    TestContext.Current.CancellationToken));
            controller.Cancel();
            await pending;
            Assert.False(controller.IsRunning);
        }
    }
}
