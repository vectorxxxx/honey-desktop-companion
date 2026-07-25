using Honey.Integrations.Windows;
using Honey.Rendering.Spider;

namespace Honey.Desktop;

public readonly record struct OverlaySize(double Width, double Height);

public static class OverlaySizePolicy
{
    public static OverlaySize Calculate(float scale)
    {
        var viewport = SpiderViewportMetrics.ForScale(scale);
        return new OverlaySize(viewport.Width, viewport.Height);
    }

    public static PixelRect KeepCenter(PixelRect current, OverlaySize next)
    {
        var width = Math.Max(1, (int)Math.Round(next.Width));
        var height = Math.Max(1, (int)Math.Round(next.Height));
        return new PixelRect(
            current.X + (current.Width - width) / 2,
            current.Y + (current.Height - height) / 2,
            width,
            height);
    }
}
