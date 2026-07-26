using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Runtime;
using Honey.Desktop.Status;
using Honey.Domain.Activity;
using Honey.Domain.Behavior;

namespace Honey.Desktop.Tests;

public sealed class PetStatusProjectorTests
{
    [Fact]
    public void 投影会输出五项百分制需求与最近六条活动()
    {
        var now = DateTimeOffset.Parse("2026-07-26T08:00:30Z");
        var state = new WhiteJadeSpiderPack().CreateInitialState(now) with
        {
            Needs = new(0.42, 0.78, 0.86, 0.64, 0.18)
        };
        var active = new ActiveBehaviorState(
            new BehaviorKey("observe"),
            "observe.track",
            BehaviorOrigin.LocalAutonomy,
            now - TimeSpan.FromSeconds(12));
        var entries = Enumerable.Range(0, 8)
            .Select(index => new PetActivityEntry(
                now.AddSeconds(-index),
                new BehaviorKey($"event-{index}"),
                BehaviorOrigin.LocalAutonomy,
                PetActivityOutcome.Started))
            .ToArray();

        var snapshot = PetStatusProjector.Project(state, active, entries, now);

        Assert.Collection(
            snapshot.Needs,
            gauge => Assert.Equal(("饥饿", 42, false), (gauge.Name, gauge.Value, gauge.HighIsGood)),
            gauge => Assert.Equal(("精力", 78, true), (gauge.Name, gauge.Value, gauge.HighIsGood)),
            gauge => Assert.Equal(("好奇", 86, true), (gauge.Name, gauge.Value, gauge.HighIsGood)),
            gauge => Assert.Equal(("亲密", 64, true), (gauge.Name, gauge.Value, gauge.HighIsGood)),
            gauge => Assert.Equal(("压力", 18, false), (gauge.Name, gauge.Value, gauge.HighIsGood)));
        Assert.Equal(6, snapshot.RecentActivities.Count);
        Assert.Equal(TimeSpan.FromSeconds(12), snapshot.BehaviorDuration);
    }

    [Fact]
    public void 未来开始时间会被压为零时长()
    {
        var now = DateTimeOffset.Parse("2026-07-26T08:00:30Z");
        var state = new WhiteJadeSpiderPack().CreateInitialState(now);
        var active = new ActiveBehaviorState(
            new BehaviorKey("observe"),
            "observe.track",
            BehaviorOrigin.SystemSchedule,
            now.AddSeconds(1));

        var snapshot = PetStatusProjector.Project(state, active, [], now);

        Assert.Equal(TimeSpan.Zero, snapshot.BehaviorDuration);
    }
}
