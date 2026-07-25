using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Runtime;
using Honey.Desktop.Settings;

namespace Honey.Desktop.Tests;

public sealed class PetRuntimeControllerTests
{
    [Fact]
    public void Tick_推进技能并将阶段写入渲染快照()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var state = new WhiteJadeSpiderPack().CreateInitialState(time.GetUtcNow());
        using var runtime = new PetRuntimeController(
            state, new AppSettings(), time, new Random(7), startTimer: false);

        var first = runtime.Tick(TimeSpan.Zero);
        time.Advance(TimeSpan.FromSeconds(1));
        var later = runtime.Tick(TimeSpan.FromSeconds(1));

        Assert.NotEmpty(first.Behavior);
        Assert.NotEmpty(later.Phase);
        Assert.True(
            later.Phase != first.Phase
            || later.PhaseProgress > first.PhaseProgress);
    }

    [Fact]
    public void SetPaused_停止自主技能推进但需求继续变化()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var state = new WhiteJadeSpiderPack().CreateInitialState(time.GetUtcNow());
        using var runtime = new PetRuntimeController(
            state, new AppSettings(), time, new Random(7), startTimer: false);
        var first = runtime.Tick(TimeSpan.Zero);
        var hunger = runtime.State.Needs.Hunger;
        runtime.SetPaused(true);
        time.Advance(TimeSpan.FromSeconds(1));

        var paused = runtime.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(first.Behavior, paused.Behavior);
        Assert.True(runtime.State.Needs.Hunger > hunger);
    }

    [Fact]
    public void ApplySettings_立即应用尺寸与强制狂暴模式()
    {
        var state = new WhiteJadeSpiderPack().CreateInitialState(DateTimeOffset.UtcNow);
        using var runtime = new PetRuntimeController(
            state, new AppSettings(), random: new Random(2), startTimer: false);

        runtime.ApplySettings(new AppSettings { PetSize = 240, ModePreference = "berserk" });

        Assert.Equal(240d / 140d, runtime.State.Scale, 8);
        Assert.Equal(Honey.Domain.Model.PetMode.Berserk, runtime.State.Mode);
    }

    [Fact]
    public void Tick_一秒采用不同帧分割会得到相同状态与技能帧()
    {
        var sixty = CreateRuntime();
        var ten = CreateRuntime();
        var one = CreateRuntime();

        Honey.Rendering.RenderSnapshot? sixtyFrame = null;
        var slice = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
        for (var index = 0; index < 59; index++)
        {
            sixtyFrame = sixty.Runtime.Tick(slice);
        }

        sixtyFrame = sixty.Runtime.Tick(
            TimeSpan.FromSeconds(1) - slice * 59);
        Honey.Rendering.RenderSnapshot? tenFrame = null;
        for (var index = 0; index < 10; index++)
        {
            tenFrame = ten.Runtime.Tick(TimeSpan.FromMilliseconds(100));
        }

        var oneFrame = one.Runtime.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(one.Runtime.State, ten.Runtime.State);
        Assert.Equal(one.Runtime.State, sixty.Runtime.State);
        Assert.Equal(oneFrame.Behavior, tenFrame!.Behavior);
        Assert.Equal(oneFrame.Phase, sixtyFrame!.Phase);
        Assert.Equal(oneFrame.PhaseProgress, tenFrame.PhaseProgress, 8);
        sixty.Runtime.Dispose();
        ten.Runtime.Dispose();
        one.Runtime.Dispose();
    }

    [Fact]
    public void Tick_暂停时十六毫秒连续调用只在低频边界发布()
    {
        var fixture = CreateRuntime();
        var published = 0;
        fixture.Runtime.SnapshotChanged += (_, _) => published++;
        fixture.Runtime.SetPaused(true);

        for (var index = 0; index < 100; index++)
        {
            fixture.Runtime.Tick(TimeSpan.FromMilliseconds(16));
        }

        Assert.InRange(published, 1, 2);
        Assert.True(fixture.Runtime.State.UpdatedAt >= fixture.Initial.UpdatedAt + TimeSpan.FromSeconds(1));
        fixture.Runtime.Dispose();
    }

    [Fact]
    public void Tick_隐藏时不发布渲染且超大步长限制追赶预算()
    {
        var fixture = CreateRuntime();
        var published = 0;
        fixture.Runtime.SnapshotChanged += (_, _) => published++;
        fixture.Runtime.SetHidden(true);

        fixture.Runtime.Tick(TimeSpan.FromMinutes(5));

        Assert.Equal(0, published);
        Assert.Equal(
            TimeSpan.FromSeconds(4),
            fixture.Runtime.State.UpdatedAt - fixture.Initial.UpdatedAt);
        fixture.Runtime.Dispose();
    }

    [Fact]
    public void Tick_负时长明确拒绝()
    {
        var fixture = CreateRuntime();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => fixture.Runtime.Tick(TimeSpan.FromTicks(-1)));
        fixture.Runtime.Dispose();
    }

    private static (PetRuntimeController Runtime, Honey.Domain.Model.PetState Initial) CreateRuntime()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var initial = new WhiteJadeSpiderPack().CreateInitialState(time.GetUtcNow()) with
        {
            PetId = Guid.Parse("8cb14d36-22e0-4ef9-a760-847c4f22cddb")
        };
        return (
            new PetRuntimeController(
                initial,
                new AppSettings(),
                time,
                new Random(17),
                startTimer: false),
            initial);
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan elapsed) => current += elapsed;
    }
}
