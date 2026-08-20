namespace Honey.Desktop.Tests;

public sealed class LicenseResourceTests
{
    [Fact]
    public void 最终程序集嵌入项目与第三方完整许可原文()
    {
        var resources = typeof(Honey.Desktop.App).Assembly
            .GetManifestResourceNames()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Honey.LICENSE", resources);
        Assert.Contains("Honey.THIRD-PARTY-NOTICES.md", resources);
        Assert.Contains("Honey.LICENSES.Apache-2.0.txt", resources);
        Assert.Contains("Honey.LICENSES.MIT-Microsoft.NET.txt", resources);
        Assert.Contains("Honey.LICENSES.Zlib-GLFW.md", resources);
        Assert.Contains("Honey.LICENSES.SQLite-Public-Domain.txt", resources);
        Assert.Contains(
            "Honey.LICENSES.SkiaSharp.NativeAssets.Win32-THIRD-PARTY-NOTICES.txt",
            resources);
    }
}
