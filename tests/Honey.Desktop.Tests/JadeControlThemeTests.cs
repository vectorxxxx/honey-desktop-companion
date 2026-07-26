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
            "JadeDisabledForegroundBrush",
            "JadeDisabledSurfaceBrush",
            "JadeErrorBrush",
            "JadePrimaryActionBrush",
            "JadePrimaryActionHoverBrush",
            "JadePrimaryActionPressedBrush",
            "JadeTransparentBrush",
        ];

        Assert.All(required, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void 禁用语义保持可读颜色且不叠加过低透明度()
    {
        var document = LoadTheme();
        var disabledOpacitySetters = document
            .Descendants(Presentation + "Trigger")
            .Where(trigger =>
                string.Equals(
                    (string?)trigger.Attribute("Property"),
                    "IsEnabled",
                    StringComparison.Ordinal) &&
                string.Equals(
                    (string?)trigger.Attribute("Value"),
                    "False",
                    StringComparison.Ordinal))
            .SelectMany(trigger => trigger.Elements(Presentation + "Setter"))
            .Where(setter => string.Equals(
                (string?)setter.Attribute("Property"),
                "Opacity",
                StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(8, disabledOpacitySetters.Length);
        Assert.All(
            disabledOpacitySetters,
            setter => Assert.Equal("0.84", GetAttribute(setter, "Value")));

        var foreground = GetBrushColor(
            document,
            "JadeDisabledForegroundBrush");
        var surface = GetBrushColor(document, "JadeDisabledSurfaceBrush");
        AssertContrastAtLeast(
            "禁用按钮",
            foreground,
            surface,
            4.5);
    }

    [Fact]
    public void 主操作悬停与按下状态保持正文对比度()
    {
        var document = LoadTheme();
        var text = GetBrushColor(document, "JadeTextBrush");

        AssertContrastAtLeast(
            "主操作悬停",
            text,
            GetBrushColor(document, "JadePrimaryActionHoverBrush"),
            4.5);
        AssertContrastAtLeast(
            "主操作按下",
            text,
            GetBrushColor(document, "JadePrimaryActionPressedBrush"),
            4.5);
    }

    [Fact]
    public void 设置主题计划不暴露本机用户目录()
    {
        var planPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "SettingsJadeControlThemePlan.md");
        var plan = File.ReadAllText(planPath);

        Assert.DoesNotContain(
            @"C:\Users\",
            plan,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$env:DOTNET_ROOT", plan, StringComparison.Ordinal);
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
    public void 设置窗口主题地址不依赖发布程序集名称()
    {
        var source = LoadFixture("SettingsWindow.xaml")
            .Root?
            .Element(Presentation + "Window.Resources")?
            .Element(Presentation + "ResourceDictionary")?
            .Element(Presentation + "ResourceDictionary.MergedDictionaries")?
            .Elements(Presentation + "ResourceDictionary")
            .Single(dictionary =>
                IsJadeThemeSource((string?)dictionary.Attribute("Source")))
            .Attribute("Source")?
            .Value;

        Assert.Equal(ThemeSource, source);
        Assert.DoesNotContain(
            ";component/",
            source,
            StringComparison.OrdinalIgnoreCase);
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

    private static string GetBrushColor(
        XDocument document,
        string key)
    {
        var brush = document
            .Descendants(Presentation + "SolidColorBrush")
            .Single(element => string.Equals(
                (string?)element.Attribute(Xaml + "Key"),
                key,
                StringComparison.Ordinal));
        return GetAttribute(brush, "Color");
    }

    private static void AssertContrastAtLeast(
        string state,
        string foreground,
        string background,
        double minimum)
    {
        var contrast = CalculateContrast(foreground, background);

        Assert.True(
            contrast >= minimum,
            $"{state}对比度为 {contrast:F3}:1，低于 {minimum:F1}:1。");
    }

    private static double CalculateContrast(
        string foreground,
        string background)
    {
        var foregroundLuminance = CalculateRelativeLuminance(foreground);
        var backgroundLuminance = CalculateRelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double CalculateRelativeLuminance(string color)
    {
        Assert.Matches("^#[0-9A-Fa-f]{6}$", color);
        var red = Convert.ToByte(color.Substring(1, 2), 16) / 255d;
        var green = Convert.ToByte(color.Substring(3, 2), 16) / 255d;
        var blue = Convert.ToByte(color.Substring(5, 2), 16) / 255d;

        return 0.2126 * Linearize(red) +
            0.7152 * Linearize(green) +
            0.0722 * Linearize(blue);
    }

    private static double Linearize(double component) =>
        component <= 0.04045
            ? component / 12.92
            : Math.Pow((component + 0.055) / 1.055, 2.4);
}
