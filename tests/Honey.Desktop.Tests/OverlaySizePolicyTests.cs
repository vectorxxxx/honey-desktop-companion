using Honey.Desktop;

namespace Honey.Desktop.Tests;

public sealed class OverlaySizePolicyTests
{
    [Theory]
    [InlineData(0.5f, 320)]
    [InlineData(1.0f, 320)]
    [InlineData(1.6f, 380)]
    [InlineData(2.0f, 460)]
    public void Calculate_小尺寸保留菜单空间且大尺寸扩展窗口(float scale, double minimum)
    {
        var size = OverlaySizePolicy.Calculate(scale);

        Assert.True(size.Width >= minimum);
        Assert.True(size.Height >= minimum);
        Assert.Equal(size.Width, size.Height);
    }

    [Fact]
    public void KeepCenter_改变窗口尺寸时屏幕中心不跳()
    {
        var bounds = OverlaySizePolicy.KeepCenter(
            new Honey.Integrations.Windows.PixelRect(100, 200, 320, 320),
            new OverlaySize(480, 480));

        Assert.Equal(new Honey.Integrations.Windows.PixelRect(20, 120, 480, 480), bounds);
    }
}
