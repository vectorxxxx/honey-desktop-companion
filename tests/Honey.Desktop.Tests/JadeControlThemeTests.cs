using System.Xml.Linq;

namespace Honey.Desktop.Tests;

public sealed class JadeControlThemeTests
{
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
        ];

        Assert.All(required, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void 设置窗口显式合并墨玉主题而非全局污染透明窗口()
    {
        var settingsPath = FindRepositoryFile(
            "src", "Honey.Desktop", "Settings", "SettingsWindow.xaml");
        var appPath = FindRepositoryFile("src", "Honey.Desktop", "App.xaml");
        var settings = File.ReadAllText(settingsPath);
        var app = File.ReadAllText(appPath);

        Assert.Contains(
            "Source=\"/Honey.Desktop;component/Assets/JadeControlTheme.xaml\"",
            settings,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JadeControlTheme.xaml",
            app,
            StringComparison.Ordinal);
    }

    private static XDocument LoadTheme()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "JadeControlTheme.xaml");
        return XDocument.Load(path, LoadOptions.PreserveWhitespace);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"无法从 {AppContext.BaseDirectory} 定位仓库文件：{string.Join('/', segments)}");
    }
}
