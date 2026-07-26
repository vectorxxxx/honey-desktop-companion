namespace Honey.Desktop.Tests;

public sealed class AiOriginPresentationTests
{
    [Fact]
    public void AI思想气泡包含可见来源标签()
    {
        var document = JadeThemeXmlAssertions.LoadFixture("OverlayWindow.xaml");
        var badge = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(JadeThemeXmlAssertions.Xaml + "Name")
                == "ThoughtSourceBadge");

        Assert.Equal("AI 决策", (string?)badge.Attribute("Text"));
        Assert.Equal("Collapsed", (string?)badge.Attribute("Visibility"));
    }

    [Fact]
    public void 气泡接口接受明确来源而不是从文字猜测()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OverlayWindow.xaml.cs"));

        Assert.Contains("ThoughtSource source", source, StringComparison.Ordinal);
        Assert.Contains("source == ThoughtSource.Ai", source, StringComparison.Ordinal);
    }
}
