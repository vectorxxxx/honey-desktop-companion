using Honey.Domain.Events;
using Honey.Domain.Model;

namespace Honey.Simulation.Tests;

public sealed class PetSimulationTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Step_压力达到阈值时进入狂暴模式并发布事件()
    {
        var state = CreateState(PetMode.Normal, stress: 0.91);

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(1), random01: 0.5);

        Assert.Equal(PetMode.Berserk, result.State.Mode);
        var modeChanged = Assert.Single(result.Events);
        var @event = Assert.IsType<PetModeChanged>(modeChanged);
        Assert.Equal(state.PetId, @event.PetId);
        Assert.Equal(PetMode.Normal, @event.Before);
        Assert.Equal(PetMode.Berserk, @event.After);
    }

    [Fact]
    public void Step_压力回落到阈值时恢复正常模式并发布事件()
    {
        var state = CreateState(PetMode.Berserk, stress: 0.3);

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(1), random01: 0.5);

        Assert.Equal(PetMode.Normal, result.State.Mode);
        var @event = Assert.IsType<PetModeChanged>(Assert.Single(result.Events));
        Assert.Equal(PetMode.Berserk, @event.Before);
        Assert.Equal(PetMode.Normal, @event.After);
    }

    [Fact]
    public void Step_超过一秒时限制需求变化和更新时间()
    {
        var state = CreateState(PetMode.Normal, stress: 0.2);

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(8), random01: 0.5);

        Assert.Equal(state.Needs.Hunger + 0.002, result.State.Needs.Hunger, 12);
        Assert.Equal(state.Needs.Energy - 0.001, result.State.Needs.Energy, 12);
        Assert.Equal(state.Needs.Curiosity + 0.001, result.State.Needs.Curiosity, 12);
        Assert.Equal(InitialTime.AddSeconds(1), result.State.UpdatedAt);
    }

    [Fact]
    public void Step_负步长不会改变需求或倒退时间()
    {
        var state = CreateState(PetMode.Normal, stress: 0.2) with
        {
            UpdatedAt = DateTimeOffset.MinValue
        };

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(-2), random01: 0.5);

        Assert.Equal(state.Needs, result.State.Needs);
        Assert.Equal(DateTimeOffset.MinValue, result.State.UpdatedAt);
    }

    [Fact]
    public void Step_最大时间戳遇到正步长时饱和且不抛异常()
    {
        var state = CreateState(PetMode.Normal, stress: 0.2) with
        {
            UpdatedAt = DateTimeOffset.MaxValue
        };

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(1), random01: 0.5);

        Assert.Equal(DateTimeOffset.MaxValue, result.State.UpdatedAt);
    }

    [Fact]
    public void Step_不修改输入状态且无模式变化时不发布事件()
    {
        var state = CreateState(PetMode.Normal, stress: 0.2);
        var original = state with { };

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(0.5), random01: 0.5);

        Assert.Equal(original, state);
        Assert.NotSame(state, result.State);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Step_将需求限制在零到一之间()
    {
        var state = CreateState(
            PetMode.Normal,
            stress: 0.2,
            needs: new PetNeeds(0.999, 0.0005, 0.9995, 0.5, 0.2));

        var result = new PetSimulation().Step(state, TimeSpan.FromSeconds(1), random01: 0.5);

        Assert.Equal(1, result.State.Needs.Hunger);
        Assert.Equal(0, result.State.Needs.Energy);
        Assert.Equal(1, result.State.Needs.Curiosity);
    }

    private static PetState CreateState(
        PetMode mode,
        double stress,
        PetNeeds? needs = null) =>
        new(
            Guid.Parse("92a7ded9-97a6-46dd-9324-30ab97b240c8"),
            "honey.white-jade-spider",
            needs ?? new PetNeeds(0.25, 0.85, 0.65, 0.5, stress),
            PetMood.Curious,
            mode,
            0.75,
            0.75,
            1,
            null,
            InitialTime);
}
