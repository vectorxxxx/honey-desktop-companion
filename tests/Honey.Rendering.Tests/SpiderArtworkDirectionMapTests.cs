using Honey.Rendering.Spider;

namespace Honey.Rendering.Tests;

public sealed class SpiderArtworkDirectionMapTests
{
    [Fact]
    public void 精绘方向与移动逻辑方向保持一一对应()
    {
        var mapped = Enumerable.Range(0, SpiderDirection.Count)
            .Select(index => SpiderArtworkDirectionMap.Map(
                new SpiderDirection(
                    index,
                    index * MathF.Tau / SpiderDirection.Count)))
            .Select(direction => direction.Index)
            .ToArray();

        Assert.Equal(Enumerable.Range(0, SpiderDirection.Count), mapped);
    }
}
