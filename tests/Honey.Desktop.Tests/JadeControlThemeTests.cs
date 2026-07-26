using System.Xml.Linq;

namespace Honey.Desktop.Tests;

public sealed class JadeControlThemeTests
{
    private const string ThemeSource =
        "/Honey.Desktop;component/Assets/JadeControlTheme.xaml";
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

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
    public void 组合框使用完整墨玉模板并保留键盘与弹出层部件()
    {
        var style = GetImplicitStyle("ComboBox");
        var template = GetControlTemplate(style);
        var toggleStyle = GetNamedStyle("JadeComboBoxToggleButton");

        Assert.Contains(
            toggleStyle.Descendants(Presentation + "Path"),
            path => string.Equals(
                (string?)path.Attribute("Data"),
                "M 1 1 L 5 5 L 9 1",
                StringComparison.Ordinal));
        Assert.Contains(
            template.Descendants(Presentation + "ToggleButton"),
            element =>
                string.Equals(
                    (string?)element.Attribute("IsChecked"),
                    "{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}",
                    StringComparison.Ordinal) &&
                string.Equals(
                    (string?)element.Attribute("Style"),
                    "{StaticResource JadeComboBoxToggleButton}",
                    StringComparison.Ordinal));

        AssertNamedPart(template, "PART_EditableTextBox", "TextBox");
        var popup = AssertNamedPart(template, "PART_Popup", "Popup");
        Assert.Equal("Bottom", (string?)popup.Attribute("Placement"));
        Assert.Equal("True", (string?)popup.Attribute("AllowsTransparency"));
        Assert.Equal("Fade", (string?)popup.Attribute("PopupAnimation"));
        Assert.Contains(
            popup.Descendants(Presentation + "ScrollViewer"),
            viewer => viewer.Descendants(Presentation + "ItemsPresenter").Any());
        Assert.Contains(
            template.Descendants(Presentation + "ContentPresenter"),
            presenter =>
                string.Equals(
                    (string?)presenter.Attribute("Content"),
                    "{TemplateBinding SelectionBoxItem}",
                    StringComparison.Ordinal));

        AssertTemplateTrigger(style, "IsEditable");
        AssertTemplateTrigger(style, "IsKeyboardFocusWithin");
        AssertTemplateTrigger(style, "IsEnabled");
        Assert.Contains(
            template.Descendants(Presentation + "Popup"),
            element => ((string?)element.Attribute("IsOpen"))?.Contains(
                "IsDropDownOpen",
                StringComparison.Ordinal) is true);
    }

    [Fact]
    public void 组合框列表项使用墨玉悬停选中焦点与禁用状态()
    {
        var style = GetImplicitStyle("ComboBoxItem");
        var template = GetControlTemplate(style);
        var itemBorder = AssertNamedPart(template, "ItemBorder", "Border");

        Assert.Equal("34", (string?)itemBorder.Attribute("MinHeight"));
        Assert.Equal("10,6", (string?)itemBorder.Attribute("Padding"));
        Assert.Equal("4", (string?)itemBorder.Attribute("CornerRadius"));
        Assert.Equal("3,0,0,0", (string?)itemBorder.Attribute("BorderThickness"));
        Assert.Contains(
            itemBorder.Elements(Presentation + "ContentPresenter"),
            _ => true);

        AssertTemplateTrigger(style, "IsMouseOver");
        AssertTemplateTrigger(style, "IsSelected");
        AssertTemplateTrigger(style, "IsKeyboardFocusWithin");
        AssertTemplateTrigger(style, "IsEnabled");
        Assert.Contains(
            template.Descendants(Presentation + "Setter"),
            setter =>
                string.Equals((string?)setter.Attribute("Property"), "Background", StringComparison.Ordinal) &&
                string.Equals((string?)setter.Attribute("Value"), "#173735", StringComparison.Ordinal));
    }

    [Fact]
    public void 滚动条使用无系统箭头的墨玉轨道模板()
    {
        var style = GetImplicitStyle("ScrollBar");
        var template = GetControlTemplate(style);
        var track = AssertNamedPart(template, "PART_Track", "Track");
        var horizontalTrigger = template
            .Descendants(Presentation + "Trigger")
            .SingleOrDefault(trigger =>
                string.Equals((string?)trigger.Attribute("Property"), "Orientation", StringComparison.Ordinal) &&
                string.Equals((string?)trigger.Attribute("Value"), "Horizontal", StringComparison.Ordinal));

        AssertCommandReference(template, "PageUpCommand");
        AssertCommandReference(template, "PageDownCommand");
        AssertCommandReference(template, "PageLeftCommand");
        AssertCommandReference(template, "PageRightCommand");
        Assert.Contains(
            style.Elements(Presentation + "Setter"),
            setter =>
                string.Equals((string?)setter.Attribute("Property"), "Width", StringComparison.Ordinal) &&
                string.Equals((string?)setter.Attribute("Value"), "10", StringComparison.Ordinal));
        Assert.NotNull(horizontalTrigger);
        Assert.Contains(
            horizontalTrigger.Elements(Presentation + "Setter"),
            setter =>
                string.Equals((string?)setter.Attribute("Property"), "Height", StringComparison.Ordinal) &&
                string.Equals((string?)setter.Attribute("Value"), "10", StringComparison.Ordinal));
        Assert.Contains(
            track
                .Element(Presentation + "Track.Thumb")?
                .Elements(Presentation + "Thumb") ?? [],
            _ => true);

        var thumbStyle = GetNamedStyle("JadeScrollBarThumb");
        AssertTemplateTrigger(thumbStyle, "IsMouseOver");
        AssertTemplateTrigger(thumbStyle, "IsDragging");

        var pageButtonStyle = GetNamedStyle("JadeScrollBarPageButton");
        Assert.Contains(
            pageButtonStyle.Descendants(Presentation + "Border"),
            border => string.Equals(
                (string?)border.Attribute("Background"),
                "{StaticResource JadeTransparentBrush}",
                StringComparison.Ordinal));
    }

    [Fact]
    public void 墨玉控件模板不回退到系统窗口白色画刷()
    {
        var document = LoadTheme();
        var attributeValues = document
            .Descendants()
            .Attributes()
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.DoesNotContain(
            attributeValues,
            value => value.Contains("SystemColors.WindowBrushKey", StringComparison.Ordinal));
        Assert.DoesNotContain(
            attributeValues,
            value => string.Equals(value, "White", StringComparison.OrdinalIgnoreCase));
    }

    private static XDocument LoadTheme()
        => LoadFixture("JadeControlTheme.xaml");

    private static XElement GetImplicitStyle(string targetType)
    {
        var style = LoadTheme()
            .Root?
            .Elements(Presentation + "Style")
            .SingleOrDefault(element =>
                element.Attribute(Xaml + "Key") is null &&
                string.Equals(
                    (string?)element.Attribute("TargetType"),
                    $"{{x:Type {targetType}}}",
                    StringComparison.Ordinal));

        return Assert.IsType<XElement>(style);
    }

    private static XElement GetNamedStyle(string key)
    {
        var style = LoadTheme()
            .Root?
            .Elements(Presentation + "Style")
            .SingleOrDefault(element => string.Equals(
                (string?)element.Attribute(Xaml + "Key"),
                key,
                StringComparison.Ordinal));

        return Assert.IsType<XElement>(style);
    }

    private static XElement GetControlTemplate(XElement style)
    {
        var template = style
            .Elements(Presentation + "Setter")
            .SingleOrDefault(setter => string.Equals(
                (string?)setter.Attribute("Property"),
                "Template",
                StringComparison.Ordinal))?
            .Element(Presentation + "Setter.Value")?
            .Element(Presentation + "ControlTemplate");

        return Assert.IsType<XElement>(template);
    }

    private static XElement AssertNamedPart(
        XElement template,
        string partName,
        string elementName)
    {
        var part = template
            .Descendants(Presentation + elementName)
            .SingleOrDefault(element => string.Equals(
                (string?)element.Attribute(Xaml + "Name"),
                partName,
                StringComparison.Ordinal));

        return Assert.IsType<XElement>(part);
    }

    private static void AssertTemplateTrigger(XElement style, string property)
    {
        var template = GetControlTemplate(style);

        Assert.Contains(
            template.Descendants(Presentation + "Trigger"),
            trigger => string.Equals(
                (string?)trigger.Attribute("Property"),
                property,
                StringComparison.Ordinal));
    }

    private static void AssertCommandReference(XElement template, string command)
    {
        Assert.Contains(
            template.Descendants().Attributes(),
            attribute =>
                (string.Equals(attribute.Name.LocalName, "Command", StringComparison.Ordinal) ||
                 string.Equals(attribute.Name.LocalName, "Value", StringComparison.Ordinal)) &&
                string.Equals(
                    attribute.Value,
                    $"{{x:Static ScrollBar.{command}}}",
                    StringComparison.Ordinal));
    }

    private static XDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            fileName);
        return XDocument.Load(path, LoadOptions.PreserveWhitespace);
    }

    private static bool IsJadeThemeSource(string? source) =>
        string.Equals(
            source?
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(),
            "JadeControlTheme.xaml",
            StringComparison.Ordinal);
}
