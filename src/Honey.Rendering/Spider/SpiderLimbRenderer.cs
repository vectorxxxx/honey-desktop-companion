using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed class SpiderLimbRenderer : IDisposable
{
    private readonly SKPaint _segmentFillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private readonly SKPaint _edgePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeJoin = SKStrokeJoin.Round
    };
    private readonly SKPaint _segmentHighlightPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };
    private readonly SKPaint _jointFillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private readonly SKPaint _jointHighlightPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };
    private int _disposed;

    public void DrawSegment(
        SKCanvas canvas,
        SpiderLimbSegment segment,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        if (!segment.IsValid)
        {
            return;
        }

        using var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(segment.StartSideA);
        pathBuilder.QuadTo(
            (segment.StartSideA.X + segment.EndSideA.X) / 2,
            (segment.StartSideA.Y + segment.EndSideA.Y) / 2,
            segment.EndSideA.X,
            segment.EndSideA.Y);
        pathBuilder.LineTo(segment.EndSideB);
        pathBuilder.QuadTo(
            (segment.StartSideB.X + segment.EndSideB.X) / 2,
            (segment.StartSideB.Y + segment.EndSideB.Y) / 2,
            segment.StartSideB.X,
            segment.StartSideB.Y);
        pathBuilder.Close();
        using var path = pathBuilder.Detach();

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(
                Math.Min(segment.Start.X, segment.End.X) - segment.StartWidth,
                Math.Min(segment.Start.Y, segment.End.Y) - segment.StartWidth),
            new SKPoint(
                Math.Max(segment.Start.X, segment.End.X) + segment.StartWidth,
                Math.Max(segment.Start.Y, segment.End.Y) + segment.StartWidth),
            [palette.BodyHighlight, palette.LegSurface, palette.LegShadow],
            [0, 0.48f, 1],
            SKShaderTileMode.Clamp);
        try
        {
            _segmentFillPaint.Shader = shader;
            canvas.DrawPath(path, _segmentFillPaint);
        }
        finally
        {
            _segmentFillPaint.Shader = null;
        }

        _edgePaint.Color = CreateMutedEdge(palette.LegShadow);
        _edgePaint.StrokeWidth = Math.Max(0.8f, segment.StartWidth * 0.10f);
        canvas.DrawPath(path, _edgePaint);

        if (detailLevel >= SpiderDetailLevel.Standard)
        {
            _segmentHighlightPaint.Color = palette.BodyHighlight.WithAlpha(150);
            _segmentHighlightPaint.StrokeWidth = Math.Max(0.7f, segment.StartWidth * 0.08f);
            var scoreA = segment.StartSideA.X + segment.StartSideA.Y
                + segment.EndSideA.X + segment.EndSideA.Y;
            var scoreB = segment.StartSideB.X + segment.StartSideB.Y
                + segment.EndSideB.X + segment.EndSideB.Y;
            canvas.DrawLine(
                scoreA <= scoreB ? segment.StartSideA : segment.StartSideB,
                scoreA <= scoreB ? segment.EndSideA : segment.EndSideB,
                _segmentHighlightPaint);
        }
    }

    public void DrawJoint(
        SKCanvas canvas,
        SKPoint center,
        float angleRadians,
        float width,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        if (!IsFinite(center)
            || !float.IsFinite(angleRadians)
            || !float.IsFinite(width)
            || width <= 0)
        {
            return;
        }

        var bounds = SKRect.Create(
            center.X - width * 1.05f / 2,
            center.Y - width * 0.78f / 2,
            width * 1.05f,
            width * 0.78f);
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(
                center.X - width * 0.14f,
                center.Y - width * 0.18f),
            width * 0.72f,
            [palette.BodyHighlight, palette.LegSurface, palette.LegShadow],
            [0, 0.55f, 1],
            SKShaderTileMode.Clamp);

        canvas.Save();
        try
        {
            canvas.RotateRadians(angleRadians, center.X, center.Y);
            try
            {
                _jointFillPaint.Shader = shader;
                canvas.DrawOval(bounds, _jointFillPaint);
            }
            finally
            {
                _jointFillPaint.Shader = null;
            }

            if (detailLevel >= SpiderDetailLevel.Standard)
            {
                _jointHighlightPaint.Color = palette.BodyHighlight.WithAlpha(120);
                _jointHighlightPaint.StrokeWidth = Math.Max(0.7f, width * 0.07f);
                canvas.DrawArc(bounds, 190, 160, false, _jointHighlightPaint);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    public void DrawRootSegment(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        DrawSegment(
            canvas,
            SpiderLimbGeometry.Create(
                leg.Root,
                leg.Hip,
                leg.Width,
                leg.Width * 0.76f),
            palette,
            detailLevel);
    }

    public void DrawOuterSegments(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        var hipWidth = leg.Width * 0.76f;
        var kneeWidth = leg.Width * 0.52f;
        var tipWidth = leg.Width * 0.20f;
        DrawSegment(
            canvas,
            SpiderLimbGeometry.Create(
                leg.Hip,
                leg.Knee,
                hipWidth,
                kneeWidth),
            palette,
            detailLevel);
        DrawSegment(
            canvas,
            SpiderLimbGeometry.Create(
                leg.Knee,
                leg.Tip,
                kneeWidth,
                tipWidth),
            palette,
            detailLevel);
        DrawJoint(
            canvas,
            leg.Hip,
            Direction(leg.Hip, leg.Knee),
            hipWidth,
            palette,
            detailLevel);
        DrawJoint(
            canvas,
            leg.Knee,
            Direction(leg.Knee, leg.Tip),
            kneeWidth,
            palette,
            detailLevel);
    }

    public void DrawCompleteLeg(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        DrawRootSegment(canvas, leg, palette, detailLevel);
        DrawOuterSegments(canvas, leg, palette, detailLevel);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _segmentFillPaint.Dispose();
        _edgePaint.Dispose();
        _segmentHighlightPaint.Dispose();
        _jointFillPaint.Dispose();
        _jointHighlightPaint.Dispose();
    }

    private static SKColor CreateMutedEdge(SKColor color)
    {
        var gray = (color.Red + color.Green + color.Blue) / 3f;
        return new SKColor(
            (byte)Math.Clamp((gray * 0.68f + color.Red * 0.32f) * 0.72f, 0, 255),
            (byte)Math.Clamp((gray * 0.68f + color.Green * 0.32f) * 0.72f, 0, 255),
            (byte)Math.Clamp((gray * 0.68f + color.Blue * 0.32f) * 0.72f, 0, 255),
            (byte)Math.Min((int)color.Alpha, 220));
    }

    private static float Direction(SKPoint start, SKPoint end) =>
        MathF.Atan2(end.Y - start.Y, end.X - start.X);

    private static bool IsFinite(SKPoint point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y);

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
