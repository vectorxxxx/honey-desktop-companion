using Honey.Rendering.Spider;

namespace Honey.Rendering.Tests;

public sealed class SpiderHitMapTests
{
    [Fact]
    public void Contains_命中腹部但不命中透明角落()
    {
        var hitMap = SpiderHitMap.CreateDefault(200, 200, 1);

        Assert.True(hitMap.Contains(100, 100));
        Assert.False(hitMap.Contains(10, 10));
        Assert.False(hitMap.Contains(100, 25));
    }

    [Theory]
    [InlineData(0.6f)]
    [InlineData(1.0f)]
    [InlineData(1.6f)]
    public void Contains_不同缩放下腿部与身体保持可命中(float scale)
    {
        var hitMap = SpiderHitMap.CreateDefault(240, 240, scale);
        var layout = SpiderLayout.Create(240, 240, scale);
        var leg = layout.Legs[0];
        var legMidX = (leg.Root.X + leg.Knee.X) / 2;
        var legMidY = (leg.Root.Y + leg.Knee.Y) / 2;

        Assert.True(hitMap.Contains(layout.Center.X, layout.Center.Y));
        Assert.True(hitMap.Contains(legMidX, legMidY));
        Assert.False(hitMap.Contains(2, 2));
    }

    [Fact]
    public void CreateDefault_非法尺寸生成空命中图()
    {
        Assert.False(SpiderHitMap.CreateDefault(0, 200, 1).Contains(0, 0));
        Assert.False(SpiderHitMap.CreateDefault(-1, 200, 1).Contains(0, 0));
        Assert.False(SpiderHitMap.CreateDefault(float.NaN, 200, 1).Contains(0, 0));
    }

    [Fact]
    public void CreateDefault_非法缩放回退到一倍且非法点不命中()
    {
        var normal = SpiderHitMap.CreateDefault(200, 200, 1);
        var invalid = SpiderHitMap.CreateDefault(200, 200, float.NaN);

        Assert.Equal(
            normal.Contains(100, 100),
            invalid.Contains(100, 100));
        Assert.False(invalid.Contains(float.NaN, 100));
        Assert.False(invalid.Contains(100, float.PositiveInfinity));
    }
}
