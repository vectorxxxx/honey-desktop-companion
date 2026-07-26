using Honey.Rendering.Spider;

namespace Honey.Rendering.Tests;

public sealed class SpiderDetailLevelTests
{
    [Theory]
    [InlineData(60, SpiderDetailLevel.Compact)]
    [InlineData(89, SpiderDetailLevel.Compact)]
    [InlineData(90, SpiderDetailLevel.Standard)]
    [InlineData(179, SpiderDetailLevel.Standard)]
    [InlineData(180, SpiderDetailLevel.Showcase)]
    [InlineData(240, SpiderDetailLevel.Showcase)]
    public void 会按最终显示像素选择细节层级(
        float displayPixels,
        SpiderDetailLevel expected)
    {
        Assert.Equal(expected, SpiderDetailLevelSelector.Select(displayPixels));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1)]
    public void 非法像素尺寸会安全降级为紧凑层(float displayPixels)
    {
        Assert.Equal(
            SpiderDetailLevel.Compact,
            SpiderDetailLevelSelector.Select(displayPixels));
    }
}
