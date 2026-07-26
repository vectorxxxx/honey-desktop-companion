namespace Honey.Desktop.Tests;

public sealed class OverlayLocomotionIntegrationTests
{
    [Fact]
    public void Overlay窗口_接入运动时钟运行时快照设置与拖动重置()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(
            Path.Combine(root, "src", "Honey.Desktop", "OverlayWindow.xaml.cs"));

        Assert.Contains("_locomotionController.Tick", source, StringComparison.Ordinal);
        Assert.Contains("_locomotionController.UpdateSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("_locomotionController.UpdateSettings", source, StringComparison.Ordinal);
        Assert.Contains("_locomotionController.ResetToCurrentPosition", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Honey.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("无法定位 Honey 仓库根目录。");
    }
}
