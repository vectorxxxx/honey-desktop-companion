using Honey.Domain.Behavior;

namespace Honey.Domain.Tests;

public sealed class BehaviorKeyTests
{
    [Fact]
    public void ToString_ReturnsUnderlyingValue()
    {
        var key = new BehaviorKey("feed");

        Assert.Equal("feed", key.ToString());
    }
}
