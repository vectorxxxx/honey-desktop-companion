using Honey.Domain.Behavior;

namespace Honey.Domain.Tests;

public sealed class UtilityIntentSelectorTests
{
    [Fact]
    public void Select_ChoosesHighestScoreAndAvoidsRecentBehavior()
    {
        var selector = new UtilityIntentSelector();
        var candidates = new[]
        {
            new IntentCandidate(new("feed"), 0.85, TimeSpan.Zero),
            new IntentCandidate(new("play"), 0.90, TimeSpan.Zero),
            new IntentCandidate(new("sleep"), 0.30, TimeSpan.Zero)
        };

        var selected = selector.Select(candidates, new BehaviorKey("play"), random01: 0);

        Assert.Equal(new BehaviorKey("feed"), selected.Key);
    }

    [Fact]
    public void Select_ThrowsArgumentException_WhenCandidatesAreEmpty()
    {
        var selector = new UtilityIntentSelector();

        var exception = Assert.Throws<ArgumentException>(
            () => selector.Select([], previous: null, random01: 0));

        Assert.Equal("candidates", exception.ParamName);
    }

    [Fact]
    public void Select_ThrowsInvalidOperationException_WhenEveryCandidateIsCoolingDown()
    {
        var selector = new UtilityIntentSelector();
        var candidates = new[]
        {
            new IntentCandidate(new("feed"), 0.85, TimeSpan.FromSeconds(1)),
            new IntentCandidate(new("play"), 0.90, TimeSpan.FromSeconds(2))
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => selector.Select(candidates, previous: null, random01: 0));

        Assert.Equal("没有可用意图。", exception.Message);
    }

    [Fact]
    public void Select_UsesClampedRandomFactorToChooseBetweenCloseCandidates()
    {
        var selector = new UtilityIntentSelector();
        var candidates = new[]
        {
            new IntentCandidate(new("feed"), 0.80, TimeSpan.Zero),
            new IntentCandidate(new("play"), 0.78, TimeSpan.Zero)
        };

        var selectedWithLowRandom = selector.Select(candidates, previous: null, random01: -10);
        var selectedWithHighRandom = selector.Select(candidates, previous: null, random01: 10);

        Assert.Equal(new BehaviorKey("feed"), selectedWithLowRandom.Key);
        Assert.Equal(new BehaviorKey("play"), selectedWithHighRandom.Key);
    }

    [Fact]
    public void Select_ReturnsOriginalCandidateData()
    {
        var selector = new UtilityIntentSelector();
        var candidate = new IntentCandidate(new("feed"), 0.5, TimeSpan.Zero);

        var selected = selector.Select([candidate], previous: null, random01: 1);

        Assert.Equal(candidate, selected);
    }
}
