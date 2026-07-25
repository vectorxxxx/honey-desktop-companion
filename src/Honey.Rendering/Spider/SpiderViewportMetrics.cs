namespace Honey.Rendering.Spider;

public readonly record struct SpiderViewportSize(float Width, float Height);

public static class SpiderViewportMetrics
{
    public const float MinimumViewport = 320;
    internal const float CanonicalUnit = 60;
    private const float MaximumVisualRadiusInUnits = 1.82f;
    private const float SafetyPadding = 18;

    public static SpiderViewportSize ForScale(float scale)
    {
        var safeScale = float.IsFinite(scale) && scale > 0
            ? Math.Clamp(scale, 0.4f, 2)
            : 1;
        var radius = CanonicalUnit * safeScale * MaximumVisualRadiusInUnits + SafetyPadding;
        var side = Math.Max(MinimumViewport, MathF.Ceiling(radius * 2));
        return new SpiderViewportSize(side, side);
    }
}
