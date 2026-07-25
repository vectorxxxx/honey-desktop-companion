using Honey.Desktop.Tray;

namespace Honey.Desktop.Tests;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void Constructor_从Desktop组件加载图标且可释放()
    {
        using var tray = new TrayIconService();
    }
}
