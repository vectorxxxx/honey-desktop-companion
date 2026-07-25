using Honey.Domain.Behavior;
using Honey.Domain.Model;

namespace Honey.Domain.Tests;

public sealed class PetStateTests
{
    [Fact]
    public void With_创建新状态且保留未修改的值()
    {
        var now = new DateTimeOffset(2026, 7, 25, 10, 30, 0, TimeSpan.FromHours(8));
        var state = new PetState(
            Guid.NewGuid(),
            "test.species",
            new PetNeeds(0.2, 0.8, 0.4, 0.6, 0.1),
            PetMood.Curious,
            PetMode.Normal,
            0.25,
            0.75,
            1,
            new BehaviorKey("observe"),
            now);

        var changed = state with { Mode = PetMode.Berserk };

        Assert.Equal(PetMode.Normal, state.Mode);
        Assert.Equal(PetMode.Berserk, changed.Mode);
        Assert.Equal(state.PetId, changed.PetId);
        Assert.Equal(state.SpeciesId, changed.SpeciesId);
        Assert.Equal(state.UpdatedAt, changed.UpdatedAt);
    }
}
