using System.Text;
using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

public sealed class SingleInstanceMessageTests
{
    [Fact]
    public void TryParse_只接受严格的Show命令()
    {
        var accepted = SingleInstanceMessage.TryParse(Encoding.UTF8.GetBytes("show"), out var command);

        Assert.True(accepted);
        Assert.Equal(SingleInstanceCommand.Show, command);
    }

    [Fact]
    public void TryParse_接受严格的Shutdown命令()
    {
        var accepted = SingleInstanceMessage.TryParse(
            Encoding.UTF8.GetBytes("shutdown"),
            out var command);

        Assert.True(accepted);
        Assert.Equal(SingleInstanceCommand.Shutdown, command);
    }

    [Theory]
    [InlineData("SHOW")]
    [InlineData("show\n")]
    [InlineData(" show")]
    [InlineData("hide")]
    [InlineData("")]
    public void TryParse_拒绝非协议消息(string message)
    {
        var accepted = SingleInstanceMessage.TryParse(Encoding.UTF8.GetBytes(message), out _);

        Assert.False(accepted);
    }
}
