using System.Xml.Linq;
using static Honey.Desktop.Tests.JadeThemeXmlAssertions;

namespace Honey.Desktop.Tests;

public sealed class SettingsMovementContractTests
{
    [Fact]
    public void 设置窗口提供自主移动与跨显示器选项()
    {
        var document = LoadFixture("SettingsWindow.xaml");
        var autonomous = FindNamed(document, "AutonomousMovementCheck");
        var crossMonitor = FindNamed(document, "CrossMonitorCheck");

        Assert.Equal("启用自主移动", GetAttribute(autonomous, "Content"));
        Assert.Equal("允许跨显示器游走", GetAttribute(crossMonitor, "Content"));
        Assert.Equal(
            "{Binding IsChecked, ElementName=AutonomousMovementCheck}",
            GetAttribute(crossMonitor, "IsEnabled"));
    }

    [Fact]
    public void 设置窗口读写双向覆盖两个移动配置()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "SettingsWindow.xaml.cs"));

        Assert.Contains(
            "AutonomousMovementEnabled = AutonomousMovementCheck.IsChecked == true",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "AllowCrossMonitorRoaming = CrossMonitorCheck.IsChecked == true",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutonomousMovementCheck.IsChecked = settings.AutonomousMovementEnabled",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "CrossMonitorCheck.IsChecked = settings.AllowCrossMonitorRoaming",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void 非模态设置窗口保存成功后直接关闭而不设置对话框结果()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "SettingsWindow.xaml.cs"));

        Assert.DoesNotContain("DialogResult =", source, StringComparison.Ordinal);
        Assert.Contains(
            "await _save(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Close();",
            source,
            StringComparison.Ordinal);
    }

    private static XElement FindNamed(XDocument document, string name) =>
        Assert.Single(document.Descendants(), element =>
            string.Equals(
                (string?)element.Attribute(Xaml + "Name"),
                name,
                StringComparison.Ordinal));
}
