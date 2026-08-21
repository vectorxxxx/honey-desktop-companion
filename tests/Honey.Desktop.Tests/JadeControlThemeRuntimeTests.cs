using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace Honey.Desktop.Tests;

public sealed class JadeControlThemeRuntimeTests
{
    [Fact]
    public async Task 墨玉资源字典可在Sta线程实例化关键控件模板()
    {
        await RunOnStaThreadAsync(
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

    private static async Task RunOnStaThreadAsync(Action action)
    {
        var completion = new TaskCompletionSource<ExceptionDispatchInfo?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                ExceptionDispatchInfo? failure = null;
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }

                completion.TrySetResult(failure);
            });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var failure = await completion.Task.WaitAsync(
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
        thread.Join();
        failure?.Throw();
    }
}
