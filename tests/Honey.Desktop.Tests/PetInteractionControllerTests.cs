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
}
