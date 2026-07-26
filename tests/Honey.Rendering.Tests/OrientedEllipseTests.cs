using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class OrientedEllipseTests
{
    [Fact]
    public void 九十度旋转会交换边界宽高()
    {
        var ellipse = new OrientedEllipse(
            new SKPoint(100, 80),
            40,
            20,
            MathF.PI / 2);

        Assert.Equal(40, ellipse.Bounds.Width, 3);
        Assert.Equal(80, ellipse.Bounds.Height, 3);
    }

    [Fact]
    public void 命中会在局部坐标逆旋转后判断椭圆()
    {
        var ellipse = new OrientedEllipse(
            new SKPoint(100, 80),
            40,
            20,
            MathF.PI / 4);

        Assert.True(ellipse.Contains(new SKPoint(121, 101)));
        Assert.False(ellipse.Contains(new SKPoint(140, 40)));
    }

    [Fact]
    public void 非法半径会被拒绝()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OrientedEllipse.Create(new SKPoint(10, 10), 0, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OrientedEllipse.Create(new SKPoint(10, 10), 2, float.NaN, 0));
    }
}
