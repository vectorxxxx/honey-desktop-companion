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

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan elapsed) => current += elapsed;
    }
}
