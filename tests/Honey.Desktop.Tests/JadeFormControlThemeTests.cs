using System.Xml.Linq;
using static Honey.Desktop.Tests.JadeThemeXmlAssertions;

namespace Honey.Desktop.Tests;

public sealed class JadeFormControlThemeTests
{
    [Fact]
    public void 复选框使用双列墨玉模板并保留内容语义()
    {
        var style = GetImplicitStyle("CheckBox");
        var template = GetControlTemplate(style);
        var root = GetNamedElement(template, "CheckRoot", "Grid");
        var frame = GetNamedElement(template, "CheckFrame", "Border");
        var focusRing = GetNamedElement(template, "CheckFocusRing", "Border");
        var glyph = GetNamedElement(template, "CheckGlyph", "Path");
        var content = GetNamedElement(template, "CheckContent", "ContentPresenter");
        var columns = Assert
            .Single(root.Elements(Presentation + "Grid.ColumnDefinitions"))
            .Elements(Presentation + "ColumnDefinition")
            .ToArray();

        Assert.Equal(2, columns.Length);
        Assert.Equal("26", GetAttribute(columns[0], "Width"));
        Assert.Equal("*", GetAttribute(columns[1], "Width"));
        Assert.Equal("18", GetAttribute(frame, "Width"));
        Assert.Equal("18", GetAttribute(frame, "Height"));
        Assert.Equal("4", GetAttribute(frame, "CornerRadius"));
        Assert.Equal(
            "{StaticResource JadeSurfaceBrush}",
            GetAttribute(frame, "Background"));
        Assert.Equal("22", GetAttribute(focusRing, "Width"));
        Assert.Equal("22", GetAttribute(focusRing, "Height"));

        Assert.Equal("M 1 3.5 L 4 6 L 9 1", GetAttribute(glyph, "Data"));
        Assert.Equal(
            "{StaticResource JadeAccentStrongBrush}",
            GetAttribute(glyph, "Stroke"));
        Assert.Equal("Collapsed", GetAttribute(glyph, "Visibility"));

        Assert.Equal("1", GetAttribute(content, "Grid.Column"));
        AssertTemplateBinding(content, "Content", "Content");
        AssertTemplateBinding(content, "ContentTemplate", "ContentTemplate");
        AssertTemplateBinding(content, "ContentStringFormat", "ContentStringFormat");
        AssertTemplateBinding(
            content,
            "HorizontalAlignment",
            "HorizontalContentAlignment");
        Assert.Equal("True", GetAttribute(content, "RecognizesAccessKey"));

        var hover = GetTemplateTrigger(template, "IsMouseOver", "True");
        AssertSetter(
            hover,
            "CheckFrame",
            "BorderBrush",
            "{StaticResource JadeAccentBrush}");

        var focus = GetTemplateTrigger(template, "IsKeyboardFocused", "True");
        AssertSetter(focus, "CheckFocusRing", "BorderBrush", "#4D8ED7CA");
        AssertSetter(
            focus,
            "CheckFrame",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");

        var checkedTrigger = GetTemplateTrigger(template, "IsChecked", "True");
        AssertSetter(checkedTrigger, "CheckGlyph", "Visibility", "Visible");
        AssertSetter(checkedTrigger, "CheckFrame", "Background", "#153B36");
        AssertSetter(
            checkedTrigger,
            "CheckFrame",
            "BorderBrush",
            "{StaticResource JadeAccentBrush}");

        var disabled = GetTemplateTrigger(template, "IsEnabled", "False");
        AssertSetter(disabled, "CheckRoot", "Opacity", "0.68");
        AssertSetter(
            disabled,
            null,
            "Foreground",
            "{StaticResource JadeDisabledBrush}");
    }

    [Fact]
    public void 水平滑块保留范围连接并使用墨玉有效区与可见焦点环()
    {
        var style = GetImplicitStyle("Slider");
        var template = GetControlTemplate(style);
        var track = GetNamedElement(template, "PART_Track", "Track");
        var decrease =
            GetNamedElement(template, "DecreaseRepeatButton", "RepeatButton");
        var increase =
            GetNamedElement(template, "IncreaseRepeatButton", "RepeatButton");
        var thumb = GetNamedElement(template, "SliderThumb", "Thumb");
        var topTickBar = GetNamedElement(template, "TopTickBar", "TickBar");
        var bottomTickBar =
            GetNamedElement(template, "BottomTickBar", "TickBar");

        AssertTemplateBinding(track, "Minimum", "Minimum");
        AssertTemplateBinding(track, "Maximum", "Maximum");
        AssertTemplateBinding(track, "Value", "Value");
        AssertTemplateBinding(track, "Orientation", "Orientation");
        AssertTemplateBinding(
            track,
            "IsDirectionReversed",
            "IsDirectionReversed");
        Assert.Equal(
            "{StaticResource JadeSliderDecreaseButton}",
            GetAttribute(decrease, "Style"));
        Assert.Equal(
            "{StaticResource JadeSliderIncreaseButton}",
            GetAttribute(increase, "Style"));
        Assert.Equal(
            "{StaticResource JadeSliderThumb}",
            GetAttribute(thumb, "Style"));
        Assert.Equal(
            "{x:Static Slider.DecreaseLarge}",
            GetAttribute(decrease, "Command"));
        Assert.Equal(
            "{x:Static Slider.IncreaseLarge}",
            GetAttribute(increase, "Command"));
        AssertBinding(
            decrease,
            "CommandTarget",
            null,
            ("RelativeSource", "{RelativeSource TemplatedParent}"));
        AssertBinding(
            increase,
            "CommandTarget",
            null,
            ("RelativeSource", "{RelativeSource TemplatedParent}"));

        AssertTickBarConnections(topTickBar, "Top");
        AssertTickBarConnections(bottomTickBar, "Bottom");
        AssertBinding(
            topTickBar,
            "ReservedSpace",
            "ActualWidth",
            ("ElementName", "SliderThumb"));
        AssertBinding(
            bottomTickBar,
            "ReservedSpace",
            "ActualWidth",
            ("ElementName", "SliderThumb"));

        var topLeft = GetTemplateTrigger(template, "TickPlacement", "TopLeft");
        AssertSetter(topLeft, "TopTickBar", "Visibility", "Visible");
        var bottomRight =
            GetTemplateTrigger(template, "TickPlacement", "BottomRight");
        AssertSetter(
            bottomRight,
            "BottomTickBar",
            "Visibility",
            "Visible");
        var both = GetTemplateTrigger(template, "TickPlacement", "Both");
        AssertSetter(both, "TopTickBar", "Visibility", "Visible");
        AssertSetter(both, "BottomTickBar", "Visibility", "Visible");

        var decreaseStyle = GetNamedStyle("JadeSliderDecreaseButton");
        var decreaseTrack = GetNamedElement(
            GetControlTemplate(decreaseStyle),
            "DecreaseTrack",
            "Border");
        Assert.Equal("4", GetAttribute(decreaseTrack, "Height"));
        Assert.Equal("2", GetAttribute(decreaseTrack, "CornerRadius"));
        Assert.Equal(
            "{StaticResource JadeAccentBrush}",
            GetAttribute(decreaseTrack, "Background"));

        var increaseStyle = GetNamedStyle("JadeSliderIncreaseButton");
        var increaseTrack = GetNamedElement(
            GetControlTemplate(increaseStyle),
            "IncreaseTrack",
            "Border");
        Assert.Equal("4", GetAttribute(increaseTrack, "Height"));
        Assert.Equal("2", GetAttribute(increaseTrack, "CornerRadius"));
        Assert.Equal("#294943", GetAttribute(increaseTrack, "Background"));

        var thumbStyle = GetNamedStyle("JadeSliderThumb");
        AssertStyleSetter(thumbStyle, "Width", "22");
        AssertStyleSetter(thumbStyle, "Height", "22");
        var thumbTemplate = GetControlTemplate(thumbStyle);
        var focusRing =
            GetNamedElement(thumbTemplate, "SliderThumbFocusRing", "Border");
        var thumbCore =
            GetNamedElement(thumbTemplate, "SliderThumbCore", "Border");
        Assert.Equal("16", GetAttribute(thumbCore, "Width"));
        Assert.Equal("16", GetAttribute(thumbCore, "Height"));
        Assert.Equal("8", GetAttribute(thumbCore, "CornerRadius"));
        Assert.Equal(
            "{StaticResource JadeSurfaceBrush}",
            GetAttribute(thumbCore, "Background"));
        Assert.Equal(
            "{StaticResource JadeAccentBrush}",
            GetAttribute(thumbCore, "BorderBrush"));
        Assert.Equal("22", GetAttribute(focusRing, "Width"));
        Assert.Equal("22", GetAttribute(focusRing, "Height"));

        AssertSetter(
            GetTemplateTrigger(thumbTemplate, "IsMouseOver", "True"),
            "SliderThumbCore",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(
            GetTemplateTrigger(thumbTemplate, "IsDragging", "True"),
            "SliderThumbFocusRing",
            "BorderBrush",
            "#4D8ED7CA");

        var focus = GetTemplateTrigger(template, "IsKeyboardFocused", "True");
        AssertSetter(focus, "SliderThumb", "BorderBrush", "#4D8ED7CA");
        var disabled = GetTemplateTrigger(template, "IsEnabled", "False");
        AssertSetter(disabled, "SliderRoot", "Opacity", "0.68");
    }

    [Fact]
    public void 文本框使用内容宿主并覆盖悬停焦点只读与禁用状态()
    {
        var style = GetImplicitStyle("TextBox");
        var template = GetControlTemplate(style);
        var inputBorder = GetNamedElement(template, "InputBorder", "Border");
        var contentHost =
            GetNamedElement(template, "PART_ContentHost", "ScrollViewer");

        AssertInputDefaults(style);
        AssertTemplateBinding(inputBorder, "Background", "Background");
        AssertTemplateBinding(inputBorder, "BorderBrush", "BorderBrush");
        AssertTemplateBinding(inputBorder, "BorderThickness", "BorderThickness");
        AssertTemplateBinding(inputBorder, "Padding", "Padding");
        Assert.Equal("5", GetAttribute(inputBorder, "CornerRadius"));
        Assert.Null(contentHost.Attribute("Margin"));

        AssertSetter(
            GetTemplateTrigger(template, "IsMouseOver", "True"),
            "InputBorder",
            "BorderBrush",
            "{StaticResource JadeAccentBrush}");
        var focus = GetTemplateTrigger(
            template,
            "IsKeyboardFocusWithin",
            "True");
        AssertSetter(
            focus,
            "InputBorder",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(focus, "InputBorder", "BorderThickness", "2");
        AssertValidationErrorState(template);

        var readOnly = GetTemplateTrigger(template, "IsReadOnly", "True");
        AssertSetter(
            readOnly,
            "InputBorder",
            "Background",
            "{StaticResource JadeSurfaceBrush}");
        AssertSetter(
            readOnly,
            null,
            "Foreground",
            "{StaticResource JadeMutedTextBrush}");

        AssertInputDisabledState(template);
    }

    [Fact]
    public void 密码框使用原生内容宿主且不引入明文呈现部件()
    {
        var style = GetImplicitStyle("PasswordBox");
        var template = GetControlTemplate(style);
        var inputBorder = GetNamedElement(template, "InputBorder", "Border");
        var contentHost =
            GetNamedElement(template, "PART_ContentHost", "ScrollViewer");

        AssertInputDefaults(style);
        AssertTemplateBinding(inputBorder, "Background", "Background");
        AssertTemplateBinding(inputBorder, "BorderBrush", "BorderBrush");
        AssertTemplateBinding(inputBorder, "BorderThickness", "BorderThickness");
        AssertTemplateBinding(inputBorder, "Padding", "Padding");
        Assert.Equal("5", GetAttribute(inputBorder, "CornerRadius"));
        Assert.Null(contentHost.Attribute("Margin"));
        Assert.Empty(template.Descendants(Presentation + "TextBlock"));
        Assert.Empty(template.Descendants(Presentation + "TextBox"));

        AssertSetter(
            GetTemplateTrigger(template, "IsMouseOver", "True"),
            "InputBorder",
            "BorderBrush",
            "{StaticResource JadeAccentBrush}");
        var focus = GetTemplateTrigger(
            template,
            "IsKeyboardFocusWithin",
            "True");
        AssertSetter(
            focus,
            "InputBorder",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(focus, "InputBorder", "BorderThickness", "2");
        AssertValidationErrorState(template);
        AssertInputDisabledState(template);
    }

    [Fact]
    public void 按钮模板与动作按钮覆盖保持墨玉交互状态和保存按钮差异()
    {
        var style = GetImplicitStyle("Button");
        var template = GetControlTemplate(style);
        var border = GetNamedElement(template, "ButtonBorder", "Border");
        var focusRing = GetNamedElement(template, "ButtonFocusRing", "Border");
        var content =
            GetNamedElement(template, "ButtonContent", "ContentPresenter");

        AssertTemplateBinding(border, "Background", "Background");
        AssertTemplateBinding(border, "BorderBrush", "BorderBrush");
        AssertTemplateBinding(border, "BorderThickness", "BorderThickness");
        Assert.Equal("6", GetAttribute(border, "CornerRadius"));
        AssertTemplateBinding(content, "Content", "Content");
        AssertTemplateBinding(content, "ContentTemplate", "ContentTemplate");
        AssertTemplateBinding(content, "Margin", "Padding");
        Assert.Equal("True", GetAttribute(content, "RecognizesAccessKey"));

        var hover = GetTemplateTrigger(template, "IsMouseOver", "True");
        AssertSetter(hover, "ButtonBorder", "Background", "#2B5852");
        AssertSetter(
            hover,
            "ButtonBorder",
            "BorderBrush",
            "{StaticResource JadeAccentBrush}");

        var pressed = GetTemplateTrigger(template, "IsPressed", "True");
        AssertSetter(pressed, "ButtonBorder", "Background", "#153B36");
        AssertSetter(
            pressed,
            "ButtonBorder",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");

        var focus = GetTemplateTrigger(template, "IsKeyboardFocused", "True");
        AssertSetter(focus, "ButtonFocusRing", "BorderBrush", "#4D8ED7CA");
        Assert.Equal("2", GetAttribute(focusRing, "BorderThickness"));

        var defaulted = GetTemplateTrigger(template, "IsDefaulted", "True");
        AssertSetter(
            defaulted,
            "ButtonBorder",
            "BorderBrush",
            "{StaticResource JadeAccentStrongBrush}");
        AssertSetter(defaulted, "ButtonBorder", "BorderThickness", "2");

        var disabled = GetTemplateTrigger(template, "IsEnabled", "False");
        AssertSetter(
            disabled,
            "ButtonBorder",
            "Background",
            "{StaticResource JadeDisabledBrush}");
        AssertSetter(
            disabled,
            null,
            "Foreground",
            "{StaticResource JadeMutedTextBrush}");
        AssertSetter(disabled, "ButtonRoot", "Opacity", "0.68");

        var settings = LoadFixture("SettingsWindow.xaml");
        var actionButton = settings
            .Descendants(Presentation + "Style")
            .Single(element => string.Equals(
                (string?)element.Attribute(Xaml + "Key"),
                "ActionButton",
                StringComparison.Ordinal));
        Assert.Equal(
            "{StaticResource {x:Type Button}}",
            GetAttribute(actionButton, "BasedOn"));
        AssertStyleSetter(actionButton, "Height", "38");
        AssertStyleSetter(actionButton, "Padding", "18,0");
        AssertStyleSetter(actionButton, "Margin", "8,0,0,0");
        AssertStyleSetter(actionButton, "Cursor", "Hand");
        string[] inheritedThemeProperties =
        [
            "Foreground",
            "Background",
            "BorderBrush",
        ];
        Assert.All(
            inheritedThemeProperties,
            property => Assert.DoesNotContain(
                actionButton.Elements(Presentation + "Setter"),
                setter => string.Equals(
                    (string?)setter.Attribute("Property"),
                    property,
                    StringComparison.Ordinal)));
        Assert.Contains(
            settings.Descendants(Presentation + "Button"),
            button =>
                string.Equals(
                    (string?)button.Attribute("IsDefault"),
                    "True",
                    StringComparison.Ordinal) &&
                string.Equals(
                    (string?)button.Attribute("Background"),
                    "{StaticResource JadePrimaryActionBrush}",
                    StringComparison.Ordinal));
    }

    private static void AssertInputDefaults(XElement style)
    {
        AssertStyleSetter(
            style,
            "Background",
            "{StaticResource JadeSurfaceRaisedBrush}");
        AssertStyleSetter(
            style,
            "Foreground",
            "{StaticResource JadeTextBrush}");
        AssertStyleSetter(
            style,
            "SelectionBrush",
            "{StaticResource JadeSelectionBrush}");
        AssertStyleSetter(
            style,
            "CaretBrush",
            "{StaticResource JadeAccentStrongBrush}");
    }

    private static void AssertTickBarConnections(
        XElement tickBar,
        string placement)
    {
        Assert.Equal(placement, GetAttribute(tickBar, "Placement"));
        AssertTemplateBinding(tickBar, "Ticks", "Ticks");
        AssertTemplateBinding(tickBar, "Minimum", "Minimum");
        AssertTemplateBinding(tickBar, "Maximum", "Maximum");
        AssertTemplateBinding(tickBar, "TickFrequency", "TickFrequency");
        AssertTemplateBinding(
            tickBar,
            "IsDirectionReversed",
            "IsDirectionReversed");
        Assert.Equal("Collapsed", GetAttribute(tickBar, "Visibility"));
    }

    private static void AssertInputDisabledState(XElement template)
    {
        var disabled = GetTemplateTrigger(template, "IsEnabled", "False");
        AssertSetter(
            disabled,
            "InputBorder",
            "Background",
            "{StaticResource JadeDisabledBrush}");
        AssertSetter(
            disabled,
            "InputBorder",
            "BorderBrush",
            "{StaticResource JadeDisabledBrush}");
        AssertSetter(disabled, "InputRoot", "Opacity", "0.68");
    }

    private static void AssertValidationErrorState(XElement template)
    {
        var validationError = GetTemplateTrigger(
            template,
            "Validation.HasError",
            "True");
        AssertSetter(
            validationError,
            "InputBorder",
            "BorderBrush",
            "{StaticResource JadeErrorBrush}");
    }
}
