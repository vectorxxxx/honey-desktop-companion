using Honey.Desktop.Tray;

namespace Honey.Desktop.Tests;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void Constructor_从Desktop组件加载图标且可释放()
    {
        using var tray = new TrayIconService();
    }

    [Fact]
    public void 图标资源地址跟随发布程序集名称()
    {
        Assert.Equal(
            "/Honey;component/Assets/Honey.ico",
            TrayIconService.CreateIconResourceUri("Honey").OriginalString);
    }
}
