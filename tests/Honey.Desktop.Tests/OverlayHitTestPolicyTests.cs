using Honey.Desktop;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Tests;

public sealed class OverlayHitTestPolicyTests
{
    [Fact]
    public void Resolve_默认安全穿透()
    {
        var policy = new OverlayHitTestPolicy();

        Assert.Equal(OverlayHitTestResult.Transparent, policy.Resolve(new PixelPoint(80, 120)));
    }

    [Fact]
    public void Resolve_只有宠物命中区域接收鼠标()
    {
        var policy = new OverlayHitTestPolicy(point => point.X is >= 40 and <= 100);

        Assert.Equal(OverlayHitTestResult.Client, policy.Resolve(new PixelPoint(50, 20)));
        Assert.Equal(OverlayHitTestResult.Transparent, policy.Resolve(new PixelPoint(120, 20)));
    }
}
