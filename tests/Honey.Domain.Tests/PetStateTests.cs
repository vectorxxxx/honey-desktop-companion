using Honey.Domain.Model;

namespace Honey.Domain.Tests;

public sealed class PetStateTests
{
    [Fact]
    public void CreateWhiteJadeSpider_CreatesExpectedInitialState()
    {
        var now = new DateTimeOffset(2026, 7, 25, 10, 30, 0, TimeSpan.FromHours(8));

        var state = PetState.CreateWhiteJadeSpider(now);

        Assert.NotEqual(Guid.Empty, state.PetId);
        Assert.Equal("honey.white-jade-spider", state.SpeciesId);
        Assert.Equal(new PetNeeds(0.25, 0.85, 0.65, 0.5, 0.1), state.Needs);
        Assert.Equal(PetMood.Curious, state.Mood);
        Assert.Equal(PetMode.Normal, state.Mode);
        Assert.Equal(0.75, state.X);
        Assert.Equal(0.75, state.Y);
        Assert.Equal(1.0, state.Scale);
        Assert.Null(state.PreviousBehavior);
        Assert.Equal(now, state.UpdatedAt);
    }
}
