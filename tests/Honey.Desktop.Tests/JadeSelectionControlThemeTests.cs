using static Honey.Desktop.Tests.JadeThemeXmlAssertions;

namespace Honey.Desktop.Tests;

public sealed class JadeSelectionControlThemeTests
{
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
        Assert.Equal("Collapsed", GetAttribute(editableTextBox, "Visibility"));

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
        AssertSetter(disabledTrigger, "ComboRoot", "Opacity", "0.84");
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
        AssertSetter(triggers[3], "ItemBorder", "Opacity", "0.84");
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
}
