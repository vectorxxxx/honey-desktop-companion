using Honey.Desktop.Runtime;
using Honey.Domain.Activity;
using Honey.Domain.Behavior;

namespace Honey.Desktop.Tests;

public sealed class PetActivityJournalTests
{
    [Fact]
    public void 追加记录仅保留最近二十条且新记录在前()
    {
        var journal = new PetActivityJournal(20);
        for (var index = 0; index < 25; index++)
        {
            journal.Append(new PetActivityEntry(
                DateTimeOffset.UnixEpoch.AddSeconds(index),
                new BehaviorKey($"behavior-{index}"),
                BehaviorOrigin.LocalAutonomy,
                PetActivityOutcome.Started));
        }

        Assert.Equal(20, journal.Entries.Count);
        Assert.Equal("behavior-24", journal.Entries[0].Behavior.Value);
        Assert.Equal("behavior-5", journal.Entries[^1].Behavior.Value);
    }

    [Fact]
    public void 非正容量会被拒绝()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PetActivityJournal(0));
    }
}
