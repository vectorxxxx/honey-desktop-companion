using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace Honey.Desktop.Tests;

public sealed class JadeControlThemeRuntimeTests
{
    [Fact]
    public void 墨玉资源字典可在Sta线程实例化关键控件模板()
    {
        RunOnStaThread(
            () =>
            {
                var themePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "JadeControlTheme.xaml");
                using var stream = File.OpenRead(themePath);
                var dictionary = Assert.IsType<ResourceDictionary>(
                    XamlReader.Load(stream));

                AssertControlTemplatePart<ComboBox, Popup>(
                    dictionary,
                    "PART_Popup");
                AssertControlTemplatePart<Slider, Track>(
                    dictionary,
                    "PART_Track");
                AssertControlTemplatePart<TextBox, ScrollViewer>(
                    dictionary,
                    "PART_ContentHost");
                AssertControlTemplatePart<Button, ContentPresenter>(
                    dictionary,
                    "ButtonContent");
            });
    }

    private static void AssertControlTemplatePart<TControl, TPart>(
        ResourceDictionary dictionary,
        string partName)
        where TControl : Control, new()
        where TPart : DependencyObject
    {
        var style = Assert.IsType<Style>(dictionary[typeof(TControl)]);
        var control = new TControl
        {
            Style = style,
        };

        Assert.True(control.ApplyTemplate());
        Assert.IsType<TPart>(control.Template.FindName(partName, control));
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(
            thread.Join(TimeSpan.FromSeconds(5)),
            "STA 主题加载测试未在 5 秒内完成。");
        failure?.Throw();
    }
}
