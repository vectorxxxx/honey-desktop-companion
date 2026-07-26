using Honey.Desktop.Movement;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Tests;

public sealed class DesktopMovementBoundsTests
{
    [Fact]
    public void Create_按宠物内容边界而不是透明窗口约束原点()
    {
        var workArea = new PixelRect(0, 0, 1920, 1040);
        var content = new PixelRect(100, 80, 120, 100);

        var bounds = DesktopMovementBounds.Create(workArea, content);

        Assert.Equal(-100, bounds.Left);
        Assert.Equal(-80, bounds.Top);
        Assert.Equal(1700, bounds.Right);
        Assert.Equal(860, bounds.Bottom);
    }

    [Fact]
    public void SelectRoamingArea_默认始终选择当前显示器()
    {
        var displays = new[]
        {
            new PixelRect(0, 0, 1920, 1040),
            new PixelRect(1920, 0, 1920, 1040)
        };
        var window = new PixelRect(200, 100, 320, 320);

        var selected = DesktopMovementBounds.SelectRoamingArea(
            window,
            displays,
            allowCrossMonitor: false,
            sample: 0.99);

        Assert.Equal(displays[0], selected);
    }

    [Fact]
    public void SelectRoamingArea_允许跨屏时可选择其他显示器()
    {
        var displays = new[]
        {
            new PixelRect(0, 0, 1920, 1040),
            new PixelRect(1920, 0, 1920, 1040)
        };
        var window = new PixelRect(200, 100, 320, 320);

        var selected = DesktopMovementBounds.SelectRoamingArea(
            window,
            displays,
            allowCrossMonitor: true,
            sample: 0.99);

        Assert.Equal(displays[1], selected);
    }
}
