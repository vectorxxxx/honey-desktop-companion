using Honey.Desktop.Interaction;
using Honey.Domain.Events;

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

    [Fact]
    public void Publish_互动事件逐订阅者安全派发()
    {
        var interaction = new PetInteractionOccurred(Guid.NewGuid(), "pet");
        var received = new List<PetInteractionOccurred>();
        var errors = new List<Exception>();
        Action<PetInteractionOccurred> handlers =
            _ => throw new InvalidOperationException("首个订阅者失败");
        handlers += received.Add;

        SafeEventDispatcher.Publish(handlers, interaction, errors.Add);

        Assert.Equal([interaction], received);
        Assert.Single(errors);
    }

    [Fact]
    public void Publish_致命异常不会被吞掉()
    {
        Action<bool> handlers = _ => throw new OutOfMemoryException("致命");
        Assert.Throws<OutOfMemoryException>(
            () => SafeEventDispatcher.Publish(handlers, true));
    }
}
