using SkiaSharp;
using Honey.Domain.Behavior;

namespace Honey.Rendering.Effects;

public sealed class WebScene : IDisposable
{
    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };
    private int _disposed;

    public void Draw(SKCanvas canvas, SKRect bounds, RenderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var safe = snapshot.Normalize();
        if (!string.Equals(
                safe.Behavior,
                BuiltInBehaviorKeys.Web,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var color = safe.Mode == Domain.Model.PetMode.Berserk
            ? new SKColor(230, 68, 64, 100)
            : new SKColor(184, 248, 239, 105);
        _paint.Color = color;
        _paint.StrokeWidth = Math.Max(1, bounds.Width * 0.005f);
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
        canvas.DrawPath(path, _paint);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _paint.Dispose();
        }
    }
}
