using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Runtime;
using Honey.Desktop.Settings;
using Honey.Domain.Activity;
using Honey.Domain.Behavior;

namespace Honey.Desktop.Tests;

public sealed class BehaviorOriginIntegrationTests
{
    [Fact]
    public void 初始观察由系统调度()
    {
        using var runtime = CreateRuntime();

        Assert.Equal(BehaviorOrigin.SystemSchedule, runtime.Status.Origin);
        Assert.Contains(
            runtime.Status.RecentActivities,
            entry => entry.Origin == BehaviorOrigin.SystemSchedule
                && entry.Outcome == PetActivityOutcome.Started);
    }

    [Fact]
    public void 自主选择会记录本地来源()
    {
        using var runtime = CreateRuntime();
        runtime.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(BehaviorOrigin.LocalAutonomy, runtime.Status.Origin);
        Assert.Contains(
            runtime.Status.RecentActivities,
            entry => entry.Origin == BehaviorOrigin.LocalAutonomy
                && entry.Outcome == PetActivityOutcome.Started);
    }

    [Fact]
    public void 用户请求技能会记录用户来源()
    {
        using var runtime = CreateRuntime();
        runtime.RequestSkill(new BehaviorKey(BuiltInBehaviorKeys.Sleep));

        Assert.Equal(BehaviorOrigin.UserInteraction, runtime.Status.Origin);
    }

    [Fact]
    public void 用户用新技能替换旧技能时会记录旧行为中断()
    {
        using var runtime = CreateRuntime();
        var play = new BehaviorKey(BuiltInBehaviorKeys.Play);
        var sleep = new BehaviorKey(BuiltInBehaviorKeys.Sleep);
        runtime.RequestSkill(play);

        runtime.RequestSkill(sleep);

        Assert.Equal(sleep.Value, runtime.Status.Behavior);
        Assert.Single(
            runtime.Status.RecentActivities,
            entry => entry.Behavior == play
                && entry.Outcome == PetActivityOutcome.Interrupted);
    }

    [Fact]
    public void AI接受时记录来源而拒绝时不污染当前来源()
    {
        using var runtime = CreateRuntime();
        var accepted = runtime.TryRequestAiSkill(
            new BehaviorKey(BuiltInBehaviorKeys.Play));
        var acceptedOrigin = runtime.Status.Origin;

        var rejected = runtime.TryRequestAiSkill(
            new BehaviorKey(BuiltInBehaviorKeys.Web));

        Assert.Equal(AiSkillDecision.Accepted, accepted);
        Assert.Equal(BehaviorOrigin.AiSuggestion, acceptedOrigin);
        Assert.Equal(AiSkillDecision.Busy, rejected);
        Assert.Equal(acceptedOrigin, runtime.Status.Origin);
        Assert.Contains(
            runtime.Status.RecentActivities,
            entry => entry.Origin == BehaviorOrigin.AiSuggestion
                && entry.Outcome == PetActivityOutcome.Rejected);
    }

    [Fact]
    public void 技能播放完会记录一次完成()
    {
        using var runtime = CreateRuntime();
        runtime.RequestSkill(new BehaviorKey(BuiltInBehaviorKeys.Pounce));

        runtime.Tick(TimeSpan.FromSeconds(1.2));

        Assert.Single(
            runtime.Status.RecentActivities,
            entry => entry.Behavior == new BehaviorKey(BuiltInBehaviorKeys.Pounce)
                && entry.Outcome == PetActivityOutcome.Completed);
        Assert.Equal(BuiltInBehaviorKeys.Observe, runtime.Status.Behavior);
        Assert.Equal(BehaviorOrigin.SystemSchedule, runtime.Status.Origin);
    }

    [Fact]
    public void 抚摸和模式切换会留下用户活动但不替换当前行为()
    {
        using var runtime = CreateRuntime();
        var before = runtime.Status.Behavior;

        runtime.Pet();
        runtime.ToggleMode();

        Assert.Equal(before, runtime.Status.Behavior);
        Assert.Contains(
            runtime.Status.RecentActivities,
            entry => entry.Behavior == new BehaviorKey("pet")
                && entry.Origin == BehaviorOrigin.UserInteraction);
        Assert.Contains(
            runtime.Status.RecentActivities,
            entry => entry.Behavior == new BehaviorKey("mode")
                && entry.Origin == BehaviorOrigin.UserInteraction);
    }

    [Fact]
    public void 连续推进时状态事件最高每秒发布四次()
    {
        using var runtime = CreateRuntime();
        var published = 0;
        runtime.StatusChanged += (_, _) => published++;

        for (var index = 0; index < 100; index++)
        {
            runtime.Tick(TimeSpan.FromMilliseconds(16));
        }

        Assert.Equal(6, published);
    }

    private static PetRuntimeController CreateRuntime()
    {
        var pack = new WhiteJadeSpiderPack();
        return new PetRuntimeController(
            pack.CreateInitialState(DateTimeOffset.UtcNow),
            new AppSettings(),
            random: new Random(7),
            startTimer: false);
    }
}
