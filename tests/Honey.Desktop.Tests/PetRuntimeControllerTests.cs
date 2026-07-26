using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Runtime;
using Honey.Desktop.Settings;
using Honey.Domain.Behavior;
using Honey.Persistence;

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

    [Fact]
    public void Pet_改善亲密与压力且短反馈不会被下一模拟帧覆盖()
    {
        var fixture = CreateRuntime();
        fixture.Runtime.Pet();
        var afterPet = fixture.Runtime.State;

        fixture.Runtime.Tick(TimeSpan.FromMilliseconds(500));

        Assert.True(afterPet.Needs.Affection > fixture.Initial.Needs.Affection);
        Assert.True(afterPet.Needs.Stress < fixture.Initial.Needs.Stress);
        Assert.Equal(Honey.Domain.Model.PetMood.Happy, fixture.Runtime.State.Mood);
        fixture.Runtime.Dispose();
    }

    [Fact]
    public void RequestSkill_睡眠真实进入播放器并持续多个Tick()
    {
        var fixture = CreateRuntime();
        fixture.Runtime.RequestSkill(new BehaviorKey(BuiltInBehaviorKeys.Sleep));

        var first = fixture.Runtime.Tick(TimeSpan.FromMilliseconds(50));
        var later = fixture.Runtime.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(BuiltInBehaviorKeys.Sleep, first.Behavior);
        Assert.Equal(BuiltInBehaviorKeys.Sleep, later.Behavior);
        Assert.StartsWith("sleep.", later.Phase, StringComparison.Ordinal);
        fixture.Runtime.Dispose();
    }

    [Fact]
    public void TryRequestAiSkill_仅允许AI白名单并执行技能冷却()
    {
        var fixture = CreateRuntime();

        Assert.Equal(
            AiSkillDecision.Accepted,
            fixture.Runtime.TryRequestAiSkill(new BehaviorKey(BuiltInBehaviorKeys.Play)));
        Assert.Equal(
            AiSkillDecision.Busy,
            fixture.Runtime.TryRequestAiSkill(new BehaviorKey(BuiltInBehaviorKeys.Play)));
        Assert.Equal(
            AiSkillDecision.NotAllowed,
            fixture.Runtime.TryRequestAiSkill(new BehaviorKey(BuiltInBehaviorKeys.Pounce)));

        fixture.Runtime.Dispose();
    }

    [Fact]
    public void ToggleMode_更新状态与明确偏好且后续模拟不覆盖()
    {
        var fixture = CreateRuntime();

        var preference = fixture.Runtime.ToggleMode();
        fixture.Runtime.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal("berserk", preference);
        Assert.Equal(Honey.Domain.Model.PetMode.Berserk, fixture.Runtime.State.Mode);
        Assert.Equal("berserk", fixture.Runtime.Settings.ModePreference);
        fixture.Runtime.Dispose();
    }

    [Theory]
    [InlineData("normal", Honey.Domain.Model.PetMode.Normal)]
    [InlineData("berserk", Honey.Domain.Model.PetMode.Berserk)]
    public void Constructor_首状态立即应用尺寸和强制模式(
        string preference,
        Honey.Domain.Model.PetMode expectedMode)
    {
        var pack = new WhiteJadeSpiderPack();
        var initial = pack.CreateInitialState(DateTimeOffset.UtcNow) with
        {
            Scale = 1.8,
            Mode = expectedMode == Honey.Domain.Model.PetMode.Normal
                ? Honey.Domain.Model.PetMode.Berserk
                : Honey.Domain.Model.PetMode.Normal
        };
        using var runtime = new PetRuntimeController(
            initial,
            new AppSettings { PetSize = 98, ModePreference = preference },
            startTimer: false);

        var first = runtime.Tick(TimeSpan.Zero);

        Assert.Equal(0.7, runtime.State.Scale, 10);
        Assert.Equal(expectedMode, runtime.State.Mode);
        Assert.Equal(0.7f, first.Scale, 5);
        Assert.Equal(expectedMode, first.Mode);
    }

    [Fact]
    public void Constructor_Auto模式保留存档模式()
    {
        var pack = new WhiteJadeSpiderPack();
        var initial = pack.CreateInitialState(DateTimeOffset.UtcNow) with
        {
            Mode = Honey.Domain.Model.PetMode.Berserk
        };
        using var runtime = new PetRuntimeController(
            initial,
            new AppSettings { ModePreference = "auto" },
            startTimer: false);

        Assert.Equal(Honey.Domain.Model.PetMode.Berserk, runtime.State.Mode);
    }

    [Fact]
    public void TickFromClock_墙钟回拨不会冻结单调推进()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var initial = new WhiteJadeSpiderPack().CreateInitialState(time.GetUtcNow());
        using var runtime = new PetRuntimeController(
            initial, new AppSettings(), time, new Random(3), startTimer: false);
        time.SetUtcNow(time.GetUtcNow().AddHours(-2));
        time.AdvanceTimestamp(TimeSpan.FromSeconds(1));

        runtime.TickFromClock();

        Assert.Equal(
            TimeSpan.FromSeconds(1),
            runtime.State.UpdatedAt - initial.UpdatedAt);
    }

    [Fact]
    public async Task Commands_更新后的统一状态可保存并按同一Id加载()
    {
        var fixture = CreateRuntime();
        fixture.Runtime.Pet();
        fixture.Runtime.ToggleMode();
        fixture.Runtime.RequestSkill(new BehaviorKey(BuiltInBehaviorKeys.Sleep));
        var expected = fixture.Runtime.State;
        var database = Path.Combine(
            Path.GetTempPath(),
            "Honey.Tests",
            Guid.NewGuid().ToString("N"),
            "runtime.db");
        var store = new SqlitePetStateStore(database);

        await store.SaveAsync(expected, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(expected.PetId, TestContext.Current.CancellationToken);

        Assert.Equal(expected, loaded);
        fixture.Runtime.Dispose();
    }

    [Fact]
    public async Task StopAsync_等待计时器退出且之后不再发布帧()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
        var initial = new WhiteJadeSpiderPack().CreateInitialState(time.GetUtcNow());
        await using var runtime = new PetRuntimeController(
            initial,
            new AppSettings(),
            time,
            new Random(5),
            startTimer: true);
        var firstFrame = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var frameCount = 0;
        runtime.SnapshotChanged += (_, _) =>
        {
            Interlocked.Increment(ref frameCount);
            firstFrame.TrySetResult();
        };
        time.AdvanceTimestamp(TimeSpan.FromMilliseconds(50));

        await firstFrame.Task.WaitAsync(TestContext.Current.CancellationToken);
        await runtime.StopAsync();
        var stoppedCount = Volatile.Read(ref frameCount);
        await Task.Delay(
            TimeSpan.FromMilliseconds(80),
            TestContext.Current.CancellationToken);

        Assert.Equal(stoppedCount, Volatile.Read(ref frameCount));
        await runtime.StopAsync();
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan elapsed) => current += elapsed;
        public override long GetTimestamp() => _timestamp;
        public void SetUtcNow(DateTimeOffset value) => current = value;
        public void AdvanceTimestamp(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
