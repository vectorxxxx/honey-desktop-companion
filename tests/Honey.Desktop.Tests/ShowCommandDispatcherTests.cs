using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

public sealed class ShowCommandDispatcherTests
{
    [Fact]
    public void Handle_只投递恢复动作而不等待动作执行()
    {
        Action? queuedAction = null;
        var restored = false;
        var dispatcher = new ShowCommandDispatcher(
            isShuttingDown: () => false,
            post: action => queuedAction = action,
            restoreWindow: () => restored = true);

        var handling = dispatcher.Handle();

        Assert.True(handling.IsCompletedSuccessfully);
        Assert.False(restored);
        Assert.NotNull(queuedAction);
        queuedAction();
        Assert.True(restored);
    }

    [Fact]
    public void Handle_关停开始后忽略Show且不投递()
    {
        var posted = false;
        var dispatcher = new ShowCommandDispatcher(
            isShuttingDown: () => true,
            post: _ => posted = true,
            restoreWindow: () => throw new InvalidOperationException("不应恢复窗口"));

        var handling = dispatcher.Handle();

        Assert.True(handling.IsCompletedSuccessfully);
        Assert.False(posted);
    }

    [Fact]
    public void Handle_投递期间进入关停时安全忽略调度器拒绝()
    {
        var shuttingDown = false;
        var dispatcher = new ShowCommandDispatcher(
            isShuttingDown: () => shuttingDown,
            post: _ =>
            {
                shuttingDown = true;
                throw new InvalidOperationException("调度器已关停");
            },
            restoreWindow: () => { });

        var handling = dispatcher.Handle();

        Assert.True(handling.IsCompletedSuccessfully);
    }
}
