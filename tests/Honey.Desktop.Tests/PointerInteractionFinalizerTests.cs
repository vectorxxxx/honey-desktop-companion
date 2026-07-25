using Honey.Desktop.Interaction;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Tests;

public sealed class PointerInteractionFinalizerTests
{
    [Fact]
    public void Cancel_失捕或停用时首次返回需要恢复窗口且重复调用幂等()
    {
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => { },
            _ => { },
            dragThresholdPixels: 2);
        var finalizer = new PointerInteractionFinalizer(controller);
        controller.Begin(new PixelPoint(0, 0), new PixelPoint(20, 20));
        controller.Move(new PixelPoint(10, 10));

        Assert.True(finalizer.Cancel());
        Assert.False(finalizer.Cancel());
        Assert.False(controller.IsDragging);
    }

    [Fact]
    public void Complete_拖动结束返回需要恢复窗口而短按不返回()
    {
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => { },
            _ => { },
            dragThresholdPixels: 2);
        var finalizer = new PointerInteractionFinalizer(controller);
        controller.Begin(new PixelPoint(0, 0), new PixelPoint(20, 20));
        controller.Move(new PixelPoint(10, 10));
        Assert.True(finalizer.Complete(new PixelPoint(10, 10)));

        controller.Begin(new PixelPoint(0, 0), new PixelPoint(20, 20));
        Assert.False(finalizer.Complete(new PixelPoint(1, 1)));
    }
}
