using System.Xml.Linq;
using Honey.Desktop.Status;
using Honey.Domain.Activity;
using Honey.Domain.Behavior;

namespace Honey.Desktop.Tests;

public sealed class PetCodexWindowContractTests
{
    [Fact]
    public void 灵兽谱包含五项需求当前行为来源与最近活动()
    {
        var document = JadeThemeXmlAssertions.LoadFixture("PetCodexWindow.xaml");
        string[] names =
        [
            "HungerGauge",
            "EnergyGauge",
            "CuriosityGauge",
            "AffectionGauge",
            "StressGauge",
            "BehaviorText",
            "OriginBadge",
            "ActivityList"
        ];

        foreach (var name in names)
        {
            Assert.Single(
                document.Descendants(),
                element => (string?)element.Attribute(JadeThemeXmlAssertions.Xaml + "Name")
                    == name);
        }
    }

    [Fact]
    public void 灵兽谱来源徽章同时包含图标和文字()
    {
        var document = JadeThemeXmlAssertions.LoadFixture("PetCodexWindow.xaml");
        var badge = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(JadeThemeXmlAssertions.Xaml + "Name")
                == "OriginBadge");

        Assert.NotEmpty(badge.Descendants(JadeThemeXmlAssertions.Presentation + "Path"));
        Assert.NotEmpty(badge.Descendants(JadeThemeXmlAssertions.Presentation + "TextBlock"));
    }

    [Fact]
    public void 灵兽谱使用非模态关闭且没有DialogResult()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "PetCodexWindow.xaml.cs"));

        Assert.DoesNotContain("DialogResult", source, StringComparison.Ordinal);
        Assert.Contains("Close();", source, StringComparison.Ordinal);
        Assert.Contains("DragMove();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void 中文格式会明确显示行为来源和AI拒绝原因()
    {
        var entry = new PetActivityEntry(
            DateTimeOffset.Parse("2026-07-26T08:00:00Z"),
            new BehaviorKey("web"),
            BehaviorOrigin.AiSuggestion,
            PetActivityOutcome.Rejected,
            "技能冷却中");

        Assert.Equal("AI 建议", PetStatusText.Origin(BehaviorOrigin.AiSuggestion));
        Assert.Contains("结网", PetStatusText.Activity(entry), StringComparison.Ordinal);
        Assert.Contains("未执行", PetStatusText.Activity(entry), StringComparison.Ordinal);
        Assert.Contains("技能冷却中", PetStatusText.Activity(entry), StringComparison.Ordinal);
    }
}
