using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderLimbGeometryTests
{
    [Fact]
    public void 水平肢段会展开为预期四角并保留零角度()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(10, 20),
            new SKPoint(110, 20),
            20,
            6);

        Assert.True(segment.IsValid);
        Assert.Equal(new SKPoint(10, 10), segment.StartTop);
        Assert.Equal(new SKPoint(10, 30), segment.StartBottom);
        Assert.Equal(new SKPoint(110, 17), segment.EndTop);
        Assert.Equal(new SKPoint(110, 23), segment.EndBottom);
        Assert.Equal(0, segment.AngleRadians);
    }

    [Fact]
    public void 向下垂直肢段会使用顺时针法线并保留直角()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(20, 10),
            new SKPoint(20, 110),
            16,
            4);

        Assert.True(segment.IsValid);
        Assert.True(segment.StartTop.X > segment.StartBottom.X);
        Assert.True(segment.EndTop.X > segment.EndBottom.X);
        Assert.Equal(MathF.PI / 2, segment.AngleRadians);
    }

    [Fact]
    public void NaN点会产生无效肢段()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(float.NaN, 20),
            new SKPoint(110, 20),
            20,
            6);

        Assert.False(segment.IsValid);
    }

    [Fact]
    public void 零长度会产生无效肢段()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(20, 20),
            new SKPoint(20, 20),
            20,
            6);

        Assert.False(segment.IsValid);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void 非正宽度会产生无效肢段(float startWidth)
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(10, 20),
            new SKPoint(110, 20),
            startWidth,
            6);

        Assert.False(segment.IsValid);
    }
}
