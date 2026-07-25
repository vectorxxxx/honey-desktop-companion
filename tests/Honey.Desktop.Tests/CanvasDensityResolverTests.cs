using Honey.Desktop.Rendering;

namespace Honey.Desktop.Tests;

public sealed class CanvasDensityResolverTests
{
    [Theory]
    [InlineData(320, 320, 320, 320, 1, 1)]
    [InlineData(480, 480, 320, 320, 1.5, 1.5)]
    [InlineData(640, 640, 320, 320, 2, 2)]
    public void Resolve_优先使用一致的画布像素与Dip比例(
        float pixelWidth,
        float pixelHeight,
        double dipWidth,
        double dipHeight,
        double dpi,
        float expected)
    {
        Assert.Equal(
            expected,
            CanvasDensityResolver.Resolve(
                pixelWidth,
                pixelHeight,
                dipWidth,
                dipHeight,
                dpi,
                dpi));
    }

    [Fact]
    public void Resolve_横纵比例冲突时回退到WpfDpi()
    {
        Assert.Equal(1.5f, CanvasDensityResolver.Resolve(480, 640, 320, 320, 1.5, 1.5));
    }

    [Fact]
    public void Resolve_全部非法时回退到一倍()
    {
        Assert.Equal(1, CanvasDensityResolver.Resolve(float.NaN, 0, 0, -1, double.NaN, 0));
    }
}
