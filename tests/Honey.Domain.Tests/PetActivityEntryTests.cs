using Honey.Domain.Activity;
using Honey.Domain.Behavior;

namespace Honey.Domain.Tests;

public sealed class PetActivityEntryTests
{
    [Fact]
    public void 构造记录会保留来源结果与拒绝原因()
    {
        var at = DateTimeOffset.Parse("2026-07-26T08:00:00Z");
        var entry = new PetActivityEntry(
            at,
            new BehaviorKey("web"),
            BehaviorOrigin.AiSuggestion,
            PetActivityOutcome.Rejected,
            "技能冷却中");

        Assert.Equal(at, entry.At);
        Assert.Equal(new BehaviorKey("web"), entry.Behavior);
        Assert.Equal(BehaviorOrigin.AiSuggestion, entry.Origin);
        Assert.Equal(PetActivityOutcome.Rejected, entry.Outcome);
        Assert.Equal("技能冷却中", entry.Detail);
    }

    [Fact]
    public void 四类来源均保持稳定枚举值()
    {
        Assert.Equal(0, (int)BehaviorOrigin.LocalAutonomy);
        Assert.Equal(1, (int)BehaviorOrigin.AiSuggestion);
        Assert.Equal(2, (int)BehaviorOrigin.UserInteraction);
        Assert.Equal(3, (int)BehaviorOrigin.SystemSchedule);
    }
}
