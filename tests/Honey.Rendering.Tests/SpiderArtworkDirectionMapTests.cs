using Honey.Rendering.Spider;

namespace Honey.Rendering.Tests;

public sealed class SpiderArtworkDirectionMapTests
{
    [Fact]
    public void 原创图集校准会覆盖全部十六格且不会重复()
    {
        var mapped = Enumerable.Range(0, SpiderDirection.Count)
            .Select(index => SpiderArtworkDirectionMap.Map(
                new SpiderDirection(
                    index,
                    index * MathF.Tau / SpiderDirection.Count)))
            .Select(direction => direction.Index)
            .ToArray();

        Assert.Equal(SpiderDirection.Count, mapped.Distinct().Count());
        Assert.Equal(12, mapped[0]);
        Assert.Equal(9, mapped[2]);
        Assert.Equal(0, mapped[8]);
    }
}
