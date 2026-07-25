namespace Honey.Desktop.Rendering;

public static class CanvasDensityResolver
{
    private const float RatioTolerance = 0.05f;

    public static float Resolve(
        float pixelWidth,
        float pixelHeight,
        double dipWidth,
        double dipHeight,
        double dpiScaleX,
        double dpiScaleY)
    {
        var ratioX = Ratio(pixelWidth, dipWidth);
        var ratioY = Ratio(pixelHeight, dipHeight);
        if (ratioX > 0
            && ratioY > 0
            && Math.Abs(ratioX - ratioY) <= Math.Max(ratioX, ratioY) * RatioTolerance)
        {
            return Normalize((ratioX + ratioY) / 2);
        }

        var dpiX = Candidate(dpiScaleX);
        var dpiY = Candidate(dpiScaleY);
        if (dpiX > 0 && dpiY > 0)
        {
            return Normalize((dpiX + dpiY) / 2);
        }

        if (ratioX > 0 && ratioY <= 0)
        {
            return Normalize(ratioX);
        }

        if (ratioY > 0 && ratioX <= 0)
        {
            return Normalize(ratioY);
        }

        return 1;
    }

    private static float Ratio(float pixels, double dips) =>
        float.IsFinite(pixels)
        && pixels > 0
        && double.IsFinite(dips)
        && dips > 0
            ? pixels / (float)dips
            : 0;

    private static float Candidate(double value) =>
        double.IsFinite(value) && value > 0 ? (float)value : 0;

    private static float Normalize(float value) =>
        Honey.Rendering.Spider.SpiderViewportMetrics.NormalizeDeviceScale(value);
}
