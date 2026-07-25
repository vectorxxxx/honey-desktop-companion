using SkiaSharp;

namespace Honey.Rendering.Effects;

public sealed class WebScene
{
    public void Draw(SKCanvas canvas, SKRect bounds, RenderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var safe = snapshot.Normalize();
        if (!string.Equals(safe.Behavior, "web", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var color = safe.Mode == Domain.Model.PetMode.Berserk
            ? new SKColor(230, 68, 64, 100)
            : new SKColor(184, 248, 239, 105);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, bounds.Width * 0.005f),
            StrokeCap = SKStrokeCap.Round
        };
        using var builder = new SKPathBuilder();
        var left = bounds.Left + bounds.Width * 0.12f;
        var right = bounds.Right - bounds.Width * 0.12f;
        var top = bounds.Top + bounds.Height * 0.18f;
        var bottom = bounds.Top + bounds.Height * 0.72f;
        builder.MoveTo(left, top);
        builder.CubicTo(
            bounds.MidX - bounds.Width * 0.18f,
            top + bounds.Height * 0.12f,
            bounds.MidX + bounds.Width * 0.12f,
            top + bounds.Height * 0.08f,
            right,
            top + bounds.Height * 0.2f);
        builder.MoveTo(left + bounds.Width * 0.1f, top + bounds.Height * 0.12f);
        builder.CubicTo(
            bounds.MidX - bounds.Width * 0.12f,
            bottom,
            bounds.MidX + bounds.Width * 0.08f,
            bottom - bounds.Height * 0.05f,
            right - bounds.Width * 0.05f,
            top + bounds.Height * 0.28f);
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }
}
