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
        var toggleTemplate = GetControlTemplate(toggleStyle);
        var arrow = Assert.Single(toggleTemplate.Descendants(Presentation + "Path"));
        var toggle = GetNamedElement(template, "DropDownToggle", "ToggleButton");
        var selectionPresenter =
            GetNamedElement(template, "SelectionPresenter", "ContentPresenter");
        var editableTextBox =
            GetNamedElement(template, "PART_EditableTextBox", "TextBox");
        var popup = GetNamedElement(template, "PART_Popup", "Popup");
        var popupBorder = GetNamedElement(template, "PopupBorder", "Border");
        var popupScrollViewer =
            Assert.Single(popupBorder.Descendants(Presentation + "ScrollViewer"));

        Assert.Equal("M 1 1 L 5 5 L 9 1", GetAttribute(arrow, "Data"));
        Assert.Equal(
            "{StaticResource JadeComboBoxToggleButton}",
            GetAttribute(toggle, "Style"));
        AssertBinding(
            toggle,
            "IsChecked",
            "IsDropDownOpen",
            ("Mode", "TwoWay"),
            ("RelativeSource", "{RelativeSource TemplatedParent}"));
        AssertEffectiveValue(toggle, toggleStyle, "Focusable", "False");
        AssertEffectiveValue(toggle, toggleStyle, "ClickMode", "Press");

        Assert.Equal(
            "{TemplateBinding SelectionBoxItem}",
            GetAttribute(selectionPresenter, "Content"));
        Assert.Equal("{x:Null}", GetAttribute(editableTextBox, "Style"));

        Assert.Equal("True", GetAttribute(popup, "AllowsTransparency"));
        Assert.Equal("False", GetAttribute(popup, "Focusable"));
        AssertTemplateBinding(popup, "IsOpen", "IsDropDownOpen");
        Assert.Equal("Bottom", GetAttribute(popup, "Placement"));
        Assert.Equal("Fade", GetAttribute(popup, "PopupAnimation"));

        AssertBinding(
            popupBorder,
            "MinWidth",
            "ActualWidth",
            ("RelativeSource", "{RelativeSource TemplatedParent}"));
        Assert.Equal("280", GetAttribute(popupBorder, "MaxHeight"));
        Assert.Equal("True", GetAttribute(popupScrollViewer, "CanContentScroll"));
        Assert.Equal(
            "Disabled",
            GetAttribute(popupScrollViewer, "HorizontalScrollBarVisibility"));
        Assert.Equal(
            "Auto",
            GetAttribute(popupScrollViewer, "VerticalScrollBarVisibility"));
        Assert.Single(popupScrollViewer.Descendants(Presentation + "ItemsPresenter"));

        var editableTrigger = GetTemplateTrigger(template, "IsEditable", "True");
        AssertSetter(
            editableTrigger,
            "SelectionPresenter",
            "Visibility",
            "Collapsed");
        AssertSetter(
            editableTrigger,
            "PART_EditableTextBox",
            "Visibility",
            "Visible");

        var focusTrigger =
            GetTemplateTrigger(template, "IsKeyboardFocusWithin", "True");
        AssertSetter(
            focusTrigger,
            "ComboBorder",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(focusTrigger, "ComboBorder", "BorderThickness", "2");

        var disabledTrigger = GetTemplateTrigger(template, "IsEnabled", "False");
        AssertSetter(
            disabledTrigger,
            "ComboBorder",
            "Background",
            "{StaticResource JadeDisabledBrush}");
        AssertSetter(
            disabledTrigger,
            "ComboBorder",
            "BorderBrush",
            "{StaticResource JadeDisabledBrush}");
        AssertSetter(disabledTrigger, "ComboRoot", "Opacity", "0.68");
    }

    [Fact]
    public void 组合框列表项使用墨玉悬停选中焦点与禁用状态()
    {
        var style = GetImplicitStyle("ComboBoxItem");
        var template = GetControlTemplate(style);
        var itemBorder = GetNamedElement(template, "ItemBorder", "Border");

        Assert.Equal("34", GetAttribute(itemBorder, "MinHeight"));
        Assert.Equal("10,6", GetAttribute(itemBorder, "Padding"));
        Assert.Equal("4", GetAttribute(itemBorder, "CornerRadius"));
        Assert.Equal("3,0,0,0", GetAttribute(itemBorder, "BorderThickness"));
        Assert.Single(itemBorder.Elements(Presentation + "ContentPresenter"));

        var triggers = GetTemplateTriggers(template).ToArray();
        Assert.Equal(
            ["IsMouseOver", "IsSelected", "IsKeyboardFocusWithin", "IsEnabled"],
            triggers.Select(trigger => GetAttribute(trigger, "Property")));
        Assert.Equal(
            ["True", "True", "True", "False"],
            triggers.Select(trigger => GetAttribute(trigger, "Value")));

        AssertSetter(triggers[0], "ItemBorder", "Background", "#173735");
        AssertSetter(
            triggers[1],
            "ItemBorder",
            "Background",
            "{StaticResource JadeSelectionBrush}");
        AssertSetter(
            triggers[1],
            "ItemBorder",
            "BorderBrush",
            "{StaticResource JadeAccentBrush}");
        AssertSetter(
            triggers[1],
            null,
            "Foreground",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(
            triggers[2],
            "ItemBorder",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(
            triggers[3],
            null,
            "Foreground",
            "{StaticResource JadeDisabledBrush}");
        AssertSetter(triggers[3], "ItemBorder", "Opacity", "0.68");
    }

    [Fact]
    public void 滚动条使用无系统箭头的墨玉轨道模板()
    {
        var style = GetImplicitStyle("ScrollBar");
        var template = GetControlTemplate(style);
        var track = GetNamedElement(template, "PART_Track", "Track");
        var decreaseButton =
            GetNamedElement(template, "DecreasePageButton", "RepeatButton");
        var increaseButton =
            GetNamedElement(template, "IncreasePageButton", "RepeatButton");

        Assert.Single(
            template.Descendants(),
            element => string.Equals(
                (string?)element.Attribute(Xaml + "Name"),
                "PART_Track",
                StringComparison.Ordinal));
        AssertTemplateBinding(track, "Maximum", "Maximum");
        AssertTemplateBinding(track, "Minimum", "Minimum");
        AssertTemplateBinding(track, "Orientation", "Orientation");
        AssertTemplateBinding(track, "Value", "Value");
        AssertTemplateBinding(track, "ViewportSize", "ViewportSize");
        Assert.Equal("True", GetAttribute(track, "IsDirectionReversed"));

        Assert.Equal(
            "{x:Static ScrollBar.PageUpCommand}",
            GetAttribute(decreaseButton, "Command"));
        Assert.Equal(
            "{x:Static ScrollBar.PageDownCommand}",
            GetAttribute(increaseButton, "Command"));
        AssertBinding(
            decreaseButton,
            "CommandTarget",
            null,
            ("RelativeSource", "{RelativeSource TemplatedParent}"));
        AssertBinding(
            increaseButton,
            "CommandTarget",
            null,
            ("RelativeSource", "{RelativeSource TemplatedParent}"));

        AssertStyleSetter(style, "Width", "10");
        var horizontalTrigger =
            GetTemplateTrigger(template, "Orientation", "Horizontal");
        AssertSetter(horizontalTrigger, null, "Width", "Auto");
        AssertSetter(horizontalTrigger, null, "Height", "10");
        AssertSetter(
            horizontalTrigger,
            "PART_Track",
            "IsDirectionReversed",
            "False");
        AssertSetter(
            horizontalTrigger,
            "DecreasePageButton",
            "Command",
            "{x:Static ScrollBar.PageLeftCommand}");
        AssertSetter(
            horizontalTrigger,
            "IncreasePageButton",
            "Command",
            "{x:Static ScrollBar.PageRightCommand}");

        var trackThumbContainer =
            Assert.Single(track.Elements(Presentation + "Track.Thumb"));
        var trackThumb =
            Assert.Single(trackThumbContainer.Elements(Presentation + "Thumb"));
        Assert.Equal(
            "{StaticResource JadeScrollBarThumb}",
            GetAttribute(trackThumb, "Style"));

        var thumbStyle = GetNamedStyle("JadeScrollBarThumb");
        AssertStyleSetter(thumbStyle, "Background", "#4C7F76");
        var thumbTemplate = GetControlTemplate(thumbStyle);
        AssertSetter(
            GetTemplateTrigger(thumbTemplate, "IsMouseOver", "True"),
            "ThumbBorder",
            "Background",
            "{StaticResource JadeAccentBrush}");
        AssertSetter(
            GetTemplateTrigger(thumbTemplate, "IsDragging", "True"),
            "ThumbBorder",
            "Background",
            "{StaticResource JadeAccentStrongBrush}");

        var pageButtonStyle = GetNamedStyle("JadeScrollBarPageButton");
        var pageButtonTemplate = GetControlTemplate(pageButtonStyle);
        var pageButtonBorder =
            Assert.Single(pageButtonTemplate.Descendants(Presentation + "Border"));
        Assert.Equal(
            "{StaticResource JadeTransparentBrush}",
            GetAttribute(pageButtonBorder, "Background"));
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

    private static XElement GetNamedElement(
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

    private static IEnumerable<XElement> GetTemplateTriggers(XElement template)
    {
        var triggers = template.Element(Presentation + "ControlTemplate.Triggers");

        return Assert
            .IsType<XElement>(triggers)
            .Elements(Presentation + "Trigger");
    }

    private static XElement GetTemplateTrigger(
        XElement template,
        string property,
        string value)
    {
        var trigger = GetTemplateTriggers(template).SingleOrDefault(element =>
            string.Equals(
                (string?)element.Attribute("Property"),
                property,
                StringComparison.Ordinal) &&
            string.Equals(
                (string?)element.Attribute("Value"),
                value,
                StringComparison.Ordinal));

        return Assert.IsType<XElement>(trigger);
    }

    private static void AssertStyleSetter(
        XElement style,
        string property,
        string value) =>
        AssertSetter(style, null, property, value);

    private static void AssertEffectiveValue(
        XElement element,
        XElement style,
        string property,
        string value)
    {
        var localValue = (string?)element.Attribute(property);
        if (localValue is not null)
        {
            Assert.Equal(value, localValue);
            return;
        }

        AssertStyleSetter(style, property, value);
    }

    private static void AssertSetter(
        XElement owner,
        string? targetName,
        string property,
        string value)
    {
        var setter = owner.Elements(Presentation + "Setter").SingleOrDefault(element =>
            string.Equals(
                (string?)element.Attribute("TargetName"),
                targetName,
                StringComparison.Ordinal) &&
            string.Equals(
                (string?)element.Attribute("Property"),
                property,
                StringComparison.Ordinal));

        Assert.Equal(value, GetAttribute(Assert.IsType<XElement>(setter), "Value"));
    }

    private static void AssertTemplateBinding(
        XElement element,
        string attributeName,
        string property) =>
        Assert.Equal(
            $"{{TemplateBinding {property}}}",
            GetAttribute(element, attributeName));

    private static void AssertBinding(
        XElement element,
        string attributeName,
        string? expectedPath,
        params (string Name, string Value)[] expectedOptions)
    {
        var binding = ParseBinding(GetAttribute(element, attributeName));

        Assert.Equal(expectedPath, binding.Path);
        Assert.Equal(expectedOptions.Length, binding.Options.Count);
        Assert.All(
            expectedOptions,
            expected =>
            {
                Assert.True(
                    binding.Options.TryGetValue(expected.Name, out var actual),
                    $"绑定缺少选项 {expected.Name}。");
                Assert.Equal(expected.Value, actual);
            });
    }

    private static ParsedBinding ParseBinding(string markup)
    {
        Assert.StartsWith("{Binding", markup, StringComparison.Ordinal);
        Assert.EndsWith("}", markup, StringComparison.Ordinal);

        var body = markup["{Binding".Length..^1].Trim();
        var parts = SplitMarkupArguments(body);
        string? path = null;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
            {
                Assert.Null(path);
                path = part;
                continue;
            }

            var name = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            Assert.True(options.TryAdd(name, value), $"绑定选项 {name} 重复。");
        }

        return new ParsedBinding(
            string.IsNullOrEmpty(path) ? null : path,
            options);
    }

    private static IReadOnlyList<string> SplitMarkupArguments(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < body.Length; index++)
        {
            switch (body[index])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(body[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }

        Assert.Equal(0, depth);
        parts.Add(body[start..].Trim());
        return parts;
    }

    private static string GetAttribute(XElement element, string attributeName)
    {
        var attribute = element.Attribute(attributeName);
        return Assert.IsType<XAttribute>(attribute).Value;
    }

    private sealed record ParsedBinding(
        string? Path,
        IReadOnlyDictionary<string, string> Options);

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
