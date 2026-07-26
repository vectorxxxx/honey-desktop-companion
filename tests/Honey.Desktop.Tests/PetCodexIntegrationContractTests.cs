using System.Reflection;
using Honey.Desktop.Tray;
using Forms = System.Windows.Forms;

namespace Honey.Desktop.Tests;

public sealed class PetCodexIntegrationContractTests
{
    [Fact]
    public void Overlay提供灵兽谱请求且App维护单例窗口()
    {
        var overlay = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OverlayWindow.xaml.cs"));
        var app = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "App.xaml.cs"));

        Assert.Contains("StatusRequested", overlay, StringComparison.Ordinal);
        Assert.Contains("RuntimeStatus", overlay, StringComparison.Ordinal);
        Assert.Contains("_petCodexWindow is { IsVisible: true }", app, StringComparison.Ordinal);
        Assert.Contains("_petCodexWindow.Update", app, StringComparison.Ordinal);
        Assert.Contains("RegisterOwnWindow", app, StringComparison.Ordinal);
    }

    [Fact]
    public void 环形菜单提供可访问的灵兽谱入口()
    {
        var document = JadeThemeXmlAssertions.LoadFixture("OverlayWindow.xaml");
        Assert.Single(
            document.Descendants(),
            element => string.Equals(
                (string?)element.Attribute("AutomationProperties.Name"),
                "打开小玉灵兽谱",
                StringComparison.Ordinal));
    }

    [Fact]
    public void 托盘灵兽谱菜单会发布请求()
    {
        using var tray = new TrayIconService();
        var raised = false;
        tray.StatusRequested += (_, _) => raised = true;
        var notifyIcon = Assert.IsType<Forms.NotifyIcon>(
            typeof(TrayIconService)
                .GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(tray));
        var item = Assert.IsType<Forms.ToolStripMenuItem>(
            notifyIcon.ContextMenuStrip!.Items
                .Cast<Forms.ToolStripItem>()
                .Single(candidate => candidate.Text == "灵兽谱"));

        item.PerformClick();

        Assert.True(raised);
    }
}
