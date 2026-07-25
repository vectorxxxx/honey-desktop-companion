using Honey.Desktop.Interaction;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Tests;

public sealed class PetInteractionControllerTests
{
    [Fact]
    public void End_短按发布抚摸互动且不移动()
    {
        var petId = Guid.NewGuid();
        var interactions = new List<string>();
        var moves = new List<PixelPoint>();
        var controller = new PetInteractionController(
            petId,
            interaction => interactions.Add(interaction.Kind),
            moves.Add);

        controller.Begin(new PixelPoint(100, 100), new PixelPoint(300, 300));
        controller.End(new PixelPoint(102, 101));

        Assert.Equal(["pet"], interactions);
        Assert.Empty(moves);
        Assert.False(controller.IsDragging);
    }

    [Fact]
    public void Move_超过阈值进入拖动且按起点偏移移动窗口()
    {
        var moves = new List<PixelPoint>();
        var pauses = new List<bool>();
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => { },
            moves.Add,
            pauses.Add,
            dragThresholdPixels: 5);

        controller.Begin(new PixelPoint(100, 100), new PixelPoint(300, 300));
        controller.Move(new PixelPoint(111, 115));
        controller.End(new PixelPoint(111, 115));

        Assert.Equal([new PixelPoint(311, 315)], moves);
        Assert.Equal([true, false], pauses);
        Assert.False(controller.IsDragging);
    }

    [Fact]
    public void Move_外部回调抛普通异常时状态仍可结束并释放暂停()
    {
        var pauses = new List<bool>();
        var errors = new List<Exception>();
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => throw new InvalidOperationException("interaction"),
            _ => throw new InvalidOperationException("move"),
            paused =>
            {
                pauses.Add(paused);
                if (paused)
                {
                    throw new InvalidOperationException("pause");
                }
            },
            errorSink: errors.Add,
            dragThresholdPixels: 2);

        controller.Begin(new PixelPoint(10, 10), new PixelPoint(30, 30));
        controller.Move(new PixelPoint(20, 20));
        controller.End(new PixelPoint(20, 20));
        controller.Cancel();
        controller.Cancel();

        Assert.False(controller.IsDragging);
        Assert.Equal([true, false], pauses);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void End_互动回调异常被报告且控制器保持可复用()
    {
        var errors = new List<Exception>();
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => throw new InvalidOperationException("interaction"),
            _ => { },
            errorSink: errors.Add);

        controller.Begin(new PixelPoint(0, 0), new PixelPoint(0, 0));
        controller.End(new PixelPoint(0, 0));
        controller.Begin(new PixelPoint(0, 0), new PixelPoint(0, 0));
        controller.Cancel();

        Assert.Single(errors);
        Assert.False(controller.IsDragging);
    }

    [Fact]
    public void End_致命异常不被吞掉但内部按下状态已清理()
    {
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => throw new OutOfMemoryException("fatal"),
            _ => { });
        controller.Begin(new PixelPoint(0, 0), new PixelPoint(0, 0));

        Assert.Throws<OutOfMemoryException>(() =>
            controller.End(new PixelPoint(0, 0)));
        Assert.False(controller.IsDragging);
        Assert.False(controller.Cancel());
    }

    [Fact]
    public void End_错误报告器自身普通异常不会击穿输入事件()
    {
        var controller = new PetInteractionController(
            Guid.NewGuid(),
            _ => throw new InvalidOperationException("interaction"),
            _ => { },
            errorSink: _ => throw new InvalidOperationException("sink"));
        controller.Begin(new PixelPoint(0, 0), new PixelPoint(0, 0));

        var exception = Record.Exception(() =>
            controller.End(new PixelPoint(0, 0)));

        Assert.Null(exception);
    }
}
