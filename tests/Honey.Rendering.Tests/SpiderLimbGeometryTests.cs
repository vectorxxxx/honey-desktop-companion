using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderLimbGeometryTests
{
    [Fact]
    public void 水平肢段会展开为预期侧边并保留零角度()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(10, 20),
            new SKPoint(110, 20),
            20,
            6);

        Assert.True(segment.IsValid);
        Assert.Equal(new SKPoint(10, 10), segment.StartSideA);
        Assert.Equal(new SKPoint(10, 30), segment.StartSideB);
        Assert.Equal(new SKPoint(110, 17), segment.EndSideA);
        Assert.Equal(new SKPoint(110, 23), segment.EndSideB);
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
        Assert.True(segment.StartSideA.X > segment.StartSideB.X);
        Assert.True(segment.EndSideA.X > segment.EndSideB.X);
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
    public void Infinity点会产生无效肢段()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(float.PositiveInfinity, 20),
            new SKPoint(110, 20),
            20,
            6);

        Assert.False(segment.IsValid);
    }

    [Fact]
    public void 非有限终点会产生无效肢段()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(10, 20),
            new SKPoint(110, float.NaN),
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

    [Fact]
    public void 非正终点宽度会产生无效肢段()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(10, 20),
            new SKPoint(110, 20),
            20,
            0);

        Assert.False(segment.IsValid);
    }

    [Fact]
    public void 全有限大坐标会保持有效且所有输出有限()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(0, 0),
            new SKPoint(1e20f, 0),
            10,
            4);

        Assert.True(segment.IsValid);
        AssertAllFinite(segment);
    }

    [Fact]
    public void 有限输入但角点溢出时会产生无效肢段()
    {
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(float.MaxValue, 0),
            new SKPoint(float.MaxValue, 1),
            float.MaxValue,
            float.MaxValue);

        Assert.False(segment.IsValid);
    }

    private static void AssertAllFinite(SpiderLimbSegment segment)
    {
        Assert.True(float.IsFinite(segment.Start.X));
        Assert.True(float.IsFinite(segment.Start.Y));
        Assert.True(float.IsFinite(segment.End.X));
        Assert.True(float.IsFinite(segment.End.Y));
        Assert.True(float.IsFinite(segment.StartSideA.X));
        Assert.True(float.IsFinite(segment.StartSideA.Y));
        Assert.True(float.IsFinite(segment.StartSideB.X));
        Assert.True(float.IsFinite(segment.StartSideB.Y));
        Assert.True(float.IsFinite(segment.EndSideA.X));
        Assert.True(float.IsFinite(segment.EndSideA.Y));
        Assert.True(float.IsFinite(segment.EndSideB.X));
        Assert.True(float.IsFinite(segment.EndSideB.Y));
        Assert.True(float.IsFinite(segment.StartWidth));
        Assert.True(float.IsFinite(segment.EndWidth));
        Assert.True(float.IsFinite(segment.AngleRadians));
    }
}
