using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Movement;
using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Domain.Movement;
using Honey.Integrations.Windows;
using Honey.Rendering;

namespace Honey.Desktop.Tests;

public sealed class DesktopLocomotionControllerTests
{
    [Fact]
    public void Tick_漫游意图会移动桌宠窗口()
    {
        var origin = new PixelPoint(100, 100);
        var moves = new List<PixelPoint>();
        var controller = CreateController(() => origin, point =>
        {
            origin = point;
            moves.Add(point);
        });
        controller.UpdateSnapshot(CreateSnapshot(behavior: string.Empty));

        controller.Tick(TimeSpan.FromMilliseconds(100));

        Assert.NotEmpty(moves);
        Assert.NotEqual(new PixelPoint(100, 100), moves[^1]);
        Assert.Equal(LocomotionIntent.Roam, controller.CurrentIntent);
    }

    [Fact]
    public void Tick_暂停期间不会移动窗口()
    {
        var moves = new List<PixelPoint>();
        var controller = CreateController(
            () => new PixelPoint(100, 100),
            moves.Add);
        controller.UpdateSnapshot(CreateSnapshot(behavior: string.Empty));
        controller.SetPaused(true);

        controller.Tick(TimeSpan.FromSeconds(1));

        Assert.Empty(moves);
    }

    [Fact]
    public void ResetToCurrentPosition_拖动后以新位置重置运动状态()
    {
        var origin = new PixelPoint(100, 100);
        var controller = CreateController(() => origin, point => origin = point);
        controller.UpdateSnapshot(CreateSnapshot(behavior: string.Empty));
        controller.Tick(TimeSpan.FromMilliseconds(100));
        origin = new PixelPoint(420, 260);

        controller.ResetToCurrentPosition();

        Assert.Equal(420, controller.CurrentFrame.State.Position.X);
        Assert.Equal(260, controller.CurrentFrame.State.Position.Y);
        Assert.Equal(0, controller.CurrentFrame.State.Speed);
    }

    private static DesktopLocomotionController CreateController(
        Func<PixelPoint> getOrigin,
        Action<PixelPoint> move) =>
        new(
            getOrigin,
            () => new PixelRect(100, 80, 120, 100),
            () => [new PixelRect(0, 0, 800, 600)],
            () => new PixelPoint(700, 500),
            move,
            WhiteJadeSpiderPack.LocomotionProfile,
            new Random(7));

    private static RenderSnapshot CreateSnapshot(string behavior) =>
        new(
            PetMode.Normal,
            PetMood.Curious,
            0,
            0,
            0,
            1,
            behavior,
            BuiltInPhaseKeys.ObserveTrack);
}
