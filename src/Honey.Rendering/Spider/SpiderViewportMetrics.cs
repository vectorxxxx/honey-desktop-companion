namespace Honey.Rendering.Spider;

public readonly record struct SpiderViewportSize(float Width, float Height);

public static class SpiderViewportMetrics
{
    public const float MinimumViewport = 320;
    internal const float CanonicalUnit = 60;
    private const float MaximumTipHorizontalOffsetInUnits = 1.72f;
    private const float MaximumTipVerticalOffsetInUnits = 1.28f;
    private const float MaximumLegWidthInUnits = 0.17f;
    private const float MaximumMoodAmplitude = 1.25f;
    private const float MaximumSpeedAmplitude = 3f;
    private const float MaximumSweepRatio = 0.35f;
    private const float ContentBoundsRadiusRatio = 1.18f / 2;
    private const float ModelingMarginInUnits = 0.03f;
    // 用各轴最远足尖的保守径向上界，加上最大移动步态、关节描边半径和建模余量。
    private static readonly float MaximumVisualRadiusInUnits =
        MathF.Sqrt(
            MaximumTipHorizontalOffsetInUnits
                * MaximumTipHorizontalOffsetInUnits
            + MaximumTipVerticalOffsetInUnits
                * MaximumTipVerticalOffsetInUnits)
        + MaximumLegWidthInUnits
            * MaximumMoodAmplitude
            * MaximumSpeedAmplitude
            * MathF.Sqrt(1 + MaximumSweepRatio * MaximumSweepRatio)
        + MaximumLegWidthInUnits * ContentBoundsRadiusRatio
        + ModelingMarginInUnits;
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

    public static float NormalizeDeviceScale(float deviceScale) =>
        float.IsFinite(deviceScale) && deviceScale > 0
            ? Math.Clamp(deviceScale, 0.5f, 4)
            : 1;
}
