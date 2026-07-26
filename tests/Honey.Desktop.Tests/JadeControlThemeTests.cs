using System.Xml.Linq;
using static Honey.Desktop.Tests.JadeThemeXmlAssertions;

namespace Honey.Desktop.Tests;

public sealed class JadeControlThemeTests
{
    [Fact]
    public void 主题声明完整的墨玉语义画刷()
    {
        var document = LoadTheme();
        var keys = document
            .Descendants(Presentation + "SolidColorBrush")
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);

        string[] required =
        [
            "JadeSurfaceBrush",
            "JadeSurfaceRaisedBrush",
            "JadeBorderBrush",
            "JadeTextBrush",
            "JadeMutedTextBrush",
            "JadeAccentBrush",
            "JadeAccentStrongBrush",
            "JadeSelectionBrush",
            "JadeDisabledBrush",
            "JadeErrorBrush",
            "JadeTransparentBrush",
        ];

        Assert.All(required, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void 设置窗口显式合并墨玉主题而非全局污染透明窗口()
    {
        var settingsResources = LoadFixture("SettingsWindow.xaml")
            .Root?
            .Element(Presentation + "Window.Resources")?
            .Element(Presentation + "ResourceDictionary");
        var settingsMergedDictionaries = settingsResources?
            .Element(Presentation + "ResourceDictionary.MergedDictionaries");
        var appMergedDictionaries = LoadFixture("App.xaml")
            .Root?
            .Element(Presentation + "Application.Resources")?
            .Element(Presentation + "ResourceDictionary")?
            .Element(Presentation + "ResourceDictionary.MergedDictionaries");

        Assert.NotNull(settingsResources);
        Assert.NotNull(settingsMergedDictionaries);
        Assert.Contains(
            settingsMergedDictionaries.Elements(Presentation + "ResourceDictionary"),
            dictionary => string.Equals(
                (string?)dictionary.Attribute("Source"),
                ThemeSource,
                StringComparison.Ordinal));
        Assert.NotNull(appMergedDictionaries);
        Assert.DoesNotContain(
            appMergedDictionaries.Elements(Presentation + "ResourceDictionary"),
            dictionary => IsJadeThemeSource((string?)dictionary.Attribute("Source")));

        string[] disallowedTargetTypes =
        [
            "ComboBox",
            "{x:Type ComboBox}",
            "CheckBox",
            "{x:Type CheckBox}",
            "TextBox",
            "{x:Type TextBox}",
            "PasswordBox",
            "{x:Type PasswordBox}",
        ];
        var localImplicitTargets = settingsResources
            .Elements(Presentation + "Style")
            .Where(style => style.Attribute(Xaml + "Key") is null)
            .Select(style => (string?)style.Attribute("TargetType"))
            .Where(target => target is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(
            localImplicitTargets,
            target => disallowedTargetTypes.Contains(target, StringComparer.Ordinal));
    }

    [Fact]
    public void 墨玉控件模板不回退到系统窗口白色画刷()
    {
        var document = LoadTheme();
        var attributeValues = document.Descendants().Attributes().ToArray();
        string[] disallowedColors = ["White", "#FFF", "#FFFFFF", "#FFFFFFFF"];

        Assert.DoesNotContain(
            attributeValues,
            attribute => attribute.Value.Contains(
                "SystemColors.WindowBrushKey",
                StringComparison.OrdinalIgnoreCase));
        Assert.All(
            disallowedColors,
            color => Assert.DoesNotContain(
                attributeValues,
                attribute => string.Equals(
                    attribute.Value.Trim(),
                    color,
                    StringComparison.OrdinalIgnoreCase)));
    }
}
