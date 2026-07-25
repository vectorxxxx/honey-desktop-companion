using Honey.Integrations.Windows;

namespace Honey.Desktop.Tests;

public sealed class DisplayBoundsServiceTests
{
    [Fact]
    public void ClampWindow_将完全位于屏幕外的窗口恢复到工作区右下角()
    {
        var workArea = new PixelRect(-1920, 0, 1920, 1040);
        var window = new PixelRect(2000, 2000, 320, 320);

        var restored = DisplayBoundsService.ClampWindow(window, workArea);

        Assert.Equal(new PixelRect(-320, 720, 320, 320), restored);
    }

    [Fact]
    public void ClampWindow_窗口大于工作区时从工作区左上角开始且不改变尺寸()
    {
        var workArea = new PixelRect(0, 0, 200, 160);
        var window = new PixelRect(80, 90, 320, 240);

        var restored = DisplayBoundsService.ClampWindow(window, workArea);

        Assert.Equal(new PixelRect(0, 0, 320, 240), restored);
    }

    [Fact]
    public void FindNearestWorkArea_优先选择与窗口相交面积最大的屏幕()
    {
        var displays = new[]
        {
            new PixelRect(0, 0, 1920, 1040),
            new PixelRect(1920, 0, 1920, 1040)
        };
        var window = new PixelRect(1800, 100, 400, 300);

        var selected = DisplayBoundsService.FindNearestWorkArea(window, displays);

        Assert.Equal(displays[1], selected);
    }
}
