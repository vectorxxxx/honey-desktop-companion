using Honey.Domain.Movement;

namespace Honey.Domain.Tests;

public sealed class PetLocomotionEngineTests
{
    private static readonly LocomotionBounds Bounds = new(0, 0, 200, 120);
    private static readonly PetLocomotionProfile Profile = new(
        MaxSpeed: 100,
        Acceleration: 50,
        DecelerationRadius: 30,
        ArrivalRadius: 2,
        MaxTurnRadiansPerSecond: Math.PI / 2,
        BerserkSpeedMultiplier: 1.6,
        MaximumStep: TimeSpan.FromMilliseconds(100));

    [Fact]
    public void Step_向目标加速且不超过最大速度()
    {
        var state = LocomotionState.At(new LocomotionPoint(10, 10));
        var input = new PetLocomotionInput(
            LocomotionIntent.Roam,
            new LocomotionPoint(180, 10),
            Bounds);

        var frame = PetLocomotionEngine.Step(
            state,
            input,
            Profile,
            TimeSpan.FromMilliseconds(100));

        Assert.Equal(5, frame.State.Velocity.X, 6);
        Assert.Equal(0, frame.State.Velocity.Y, 6);
        Assert.InRange(frame.State.Speed, 0.001, Profile.MaxSpeed);
        Assert.True(frame.State.Position.X > state.Position.X);
    }

    [Fact]
    public void Step_抵达目标后停驻()
    {
        var state = LocomotionState.At(new LocomotionPoint(99, 60)) with
        {
            Velocity = new LocomotionPoint(20, 0)
        };
        var input = new PetLocomotionInput(
            LocomotionIntent.Roam,
            new LocomotionPoint(100, 60),
            Bounds);

        var frame = PetLocomotionEngine.Step(
            state,
            input,
            Profile,
            TimeSpan.FromMilliseconds(100));

        Assert.True(frame.Arrived);
        Assert.Equal(LocomotionPoint.Zero, frame.State.Velocity);
        Assert.Equal(state.Position, frame.State.Position);
    }

    [Fact]
    public void Step_转向速度受限()
    {
        var state = LocomotionState.At(new LocomotionPoint(50, 50)) with
        {
            Facing = new LocomotionPoint(1, 0)
        };
        var input = new PetLocomotionInput(
            LocomotionIntent.Roam,
            new LocomotionPoint(50, 110),
            Bounds);

        var frame = PetLocomotionEngine.Step(
            state,
            input,
            Profile,
            TimeSpan.FromMilliseconds(100));

        Assert.True(frame.State.Facing.X > 0.98);
        Assert.InRange(frame.State.Facing.Y, 0.14, 0.17);
    }

    [Fact]
    public void Step_不会越过活动边界()
    {
        var state = LocomotionState.At(new LocomotionPoint(195, 60)) with
        {
            Velocity = new LocomotionPoint(80, 0)
        };
        var input = new PetLocomotionInput(
            LocomotionIntent.Roam,
            new LocomotionPoint(300, 60),
            Bounds);

        var frame = PetLocomotionEngine.Step(
            state,
            input,
            Profile,
            TimeSpan.FromMilliseconds(100));

        Assert.Equal(Bounds.Right, frame.State.Position.X);
        Assert.Equal(0, frame.State.Velocity.X);
    }

    [Fact]
    public void Step_超大时间步长会被限制()
    {
        var state = LocomotionState.At(new LocomotionPoint(10, 10));
        var input = new PetLocomotionInput(
            LocomotionIntent.Roam,
            new LocomotionPoint(180, 10),
            Bounds);

        var longFrame = PetLocomotionEngine.Step(
            state,
            input,
            Profile,
            TimeSpan.FromSeconds(30));
        var limitedFrame = PetLocomotionEngine.Step(
            state,
            input,
            Profile,
            Profile.MaximumStep);

        Assert.Equal(limitedFrame, longFrame);
    }

    [Fact]
    public void Step_狂暴形态加速更快()
    {
        var state = LocomotionState.At(new LocomotionPoint(10, 10));
        var normalInput = new PetLocomotionInput(
            LocomotionIntent.Roam,
            new LocomotionPoint(180, 10),
            Bounds);
        var berserkInput = normalInput with { IsBerserk = true };

        var normal = PetLocomotionEngine.Step(
            state,
            normalInput,
            Profile,
            TimeSpan.FromMilliseconds(100));
        var berserk = PetLocomotionEngine.Step(
            state,
            berserkInput,
            Profile,
            TimeSpan.FromMilliseconds(100));

        Assert.True(berserk.State.Speed > normal.State.Speed);
        Assert.True(berserk.NormalizedSpeed > normal.NormalizedSpeed);
    }
}
