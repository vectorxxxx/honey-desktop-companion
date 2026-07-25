using Honey.Integrations.Windows;

namespace Honey.Integrations.Tests;

public sealed class FocusModeServiceTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, 0, 0, 1920, 1080, true)]
    [InlineData(1, 1, 1918, 1078, 0, 0, 1920, 1080, true)]
    [InlineData(100, 100, 1200, 800, 0, 0, 1920, 1080, false)]
    public void IsFullscreen_允许两像素误差(
        int x, int y, int width, int height,
        int workX, int workY, int workWidth, int workHeight,
        bool expected)
    {
        Assert.Equal(
            expected,
            FocusModeService.IsFullscreen(
                new WindowBounds(x, y, width, height),
                new WindowBounds(workX, workY, workWidth, workHeight)));
    }

    [Fact]
    public void Evaluate_忽略自身与桌面Shell()
    {
        Assert.False(FocusModeService.Evaluate(true, true, false));
        Assert.False(FocusModeService.Evaluate(true, false, true));
        Assert.True(FocusModeService.Evaluate(true, false, false));
    }

    [Fact]
    public void FocusSnapshot_锁屏会激活专注而自身窗口不会()
    {
        Assert.True(new FocusSnapshot(false, true, false, false).IsFocusModeActive);
        Assert.False(new FocusSnapshot(false, true, true, false).IsFocusModeActive);
    }
}
