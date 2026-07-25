using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

public sealed class StartupCommandInboxTests
{
    [Fact]
    public async Task 初始化期间Show会排队到就绪后且只投递一次()
    {
        var commands = new List<SingleInstanceCommand>();
        var inbox = new StartupCommandInbox(() => { });

        await inbox.HandleAsync(SingleInstanceCommand.Show);
        await inbox.HandleAsync(SingleInstanceCommand.Show);
        Assert.Empty(commands);

        await inbox.AttachAsync(command =>
        {
            commands.Add(command);
            return Task.CompletedTask;
        });

        Assert.Equal([SingleInstanceCommand.Show], commands);
    }

    [Fact]
    public async Task 初始化期间Shutdown立即取消且就绪时优先于Show()
    {
        var cancelled = 0;
        var commands = new List<SingleInstanceCommand>();
        var inbox = new StartupCommandInbox(() => Interlocked.Increment(ref cancelled));

        await inbox.HandleAsync(SingleInstanceCommand.Show);
        await inbox.HandleAsync(SingleInstanceCommand.Shutdown);
        await inbox.AttachAsync(command =>
        {
            commands.Add(command);
            return Task.CompletedTask;
        });

        Assert.Equal(1, cancelled);
        Assert.Equal([SingleInstanceCommand.Shutdown], commands);
    }
}
