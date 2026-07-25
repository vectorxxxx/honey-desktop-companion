using Honey.Integrations.Windows;

namespace Honey.Desktop;

public enum OverlayHitTestResult
{
    Transparent,
    Client
}

public sealed class OverlayHitTestPolicy
{
    private Func<PixelPoint, bool> _petHitTest;

    public OverlayHitTestPolicy(Func<PixelPoint, bool>? petHitTest = null)
    {
        _petHitTest = petHitTest ?? (_ => false);
    }

    public OverlayHitTestResult Resolve(PixelPoint point) =>
        _petHitTest(point) ? OverlayHitTestResult.Client : OverlayHitTestResult.Transparent;

    public void Update(Func<PixelPoint, bool>? petHitTest) =>
        _petHitTest = petHitTest ?? (_ => false);
}
