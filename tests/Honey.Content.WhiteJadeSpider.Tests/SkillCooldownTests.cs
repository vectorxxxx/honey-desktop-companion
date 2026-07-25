using Honey.Content.WhiteJadeSpider;
using Honey.Domain.Behavior;

namespace Honey.Content.WhiteJadeSpider.Tests;

public sealed class SkillCooldownTests
{
    [Fact]
    public void Pounce_在四十五秒后才可再次开始()
    {
        var skill = WhiteJadeSpiderSkills.All.Single(
            item => item.Key == new BehaviorKey(BuiltInBehaviorKeys.Pounce));

        Assert.True(skill.CanStart(TimeSpan.Zero));
        Assert.False(skill.CanStart(TimeSpan.FromSeconds(44)));
        Assert.True(skill.CanStart(TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public void All_技能键唯一且时间线处于声明区间()
    {
        Assert.Equal(7, WhiteJadeSpiderSkills.All.Count);
        Assert.Equal(7, WhiteJadeSpiderSkills.All.Select(skill => skill.Key).Distinct().Count());
        Assert.All(
            WhiteJadeSpiderSkills.All,
            skill =>
            {
                Assert.InRange(skill.TimelineDuration, skill.MinimumDuration, skill.MaximumDuration);
                Assert.NotEmpty(skill.Phases);
            });
    }

    [Fact]
    public void SkillPlayer_确定推进并表达当前阶段()
    {
        var skill = WhiteJadeSpiderSkills.All.Single(
            item => item.Key == new BehaviorKey(BuiltInBehaviorKeys.Pounce));
        var player = new SkillPlayer();

        player.Start(skill);
        var frame = player.Advance(TimeSpan.FromSeconds(0.5));

        Assert.Equal("短跳", frame.Phase);
        Assert.InRange(frame.Progress, 0, 1);
        Assert.False(frame.Completed);
    }
}
