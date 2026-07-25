using Honey.Desktop.Interaction;

namespace Honey.Desktop.Tests;

public sealed class SafeEventDispatcherTests
{
    [Fact]
    public void Publish_首个订阅者抛错时后续订阅者仍收到暂停状态()
    {
        var received = new List<bool>();
        var errors = new List<Exception>();
        Action<bool> handlers = _ => throw new InvalidOperationException("first");
        handlers += received.Add;

        SafeEventDispatcher.Publish(handlers, true, errors.Add);

        Assert.Equal([true], received);
        Assert.Single(errors);
    }
}
