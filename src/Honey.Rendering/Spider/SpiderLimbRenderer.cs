using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed class SpiderLimbRenderer : IDisposable
{
    private const float CurveOffsetRatio = 0.08f;
    private const double MinimumVectorLength = 0.001;
    private static readonly float[] SegmentGradientStops = [0, 0.48f, 1];
    private static readonly float[] JointGradientStops = [0, 0.55f, 1];

    private readonly object _sync = new();
    private readonly SKColor[] _gradientColors = new SKColor[3];
    private readonly SKPathBuilder _segmentPathBuilder = new();
    private readonly SKPathBuilder _jointPathBuilder = new();
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
    private readonly SKPaint _showcasePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };
    private bool _disposed;

    public void DrawSegment(
        SKCanvas canvas,
        SpiderLimbSegment segment,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        lock (_sync)
        {
            ThrowIfDisposed();
            DrawSegmentNoLock(canvas, segment, palette, detailLevel);
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
        lock (_sync)
        {
            ThrowIfDisposed();
            DrawJointNoLock(canvas, center, angleRadians, width, palette, detailLevel);
        }
    }

    public void DrawRootSegment(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        lock (_sync)
        {
            ThrowIfDisposed();
            DrawRootSegmentNoLock(canvas, leg, palette, detailLevel);
        }
    }

    public void DrawOuterSegments(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        lock (_sync)
        {
            ThrowIfDisposed();
            DrawOuterSegmentsNoLock(canvas, leg, palette, detailLevel);
        }
    }

    public void DrawCompleteLeg(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        lock (_sync)
        {
            ThrowIfDisposed();
            DrawRootSegmentNoLock(canvas, leg, palette, detailLevel);
            DrawOuterSegmentsNoLock(canvas, leg, palette, detailLevel);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _segmentPathBuilder.Dispose();
            _jointPathBuilder.Dispose();
            _segmentFillPaint.Dispose();
            _edgePaint.Dispose();
            _segmentHighlightPaint.Dispose();
            _jointFillPaint.Dispose();
            _jointHighlightPaint.Dispose();
            _showcasePaint.Dispose();
        }
    }

    private void DrawRootSegmentNoLock(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel) =>
        DrawSegmentNoLock(
            canvas,
            SpiderLimbGeometry.Create(
                leg.Root,
                leg.Hip,
                leg.Width,
                leg.Width * 0.76f),
            palette,
            detailLevel);

    private void DrawOuterSegmentsNoLock(
        SKCanvas canvas,
        SpiderLeg leg,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        var hipWidth = leg.Width * 0.76f;
        var kneeWidth = leg.Width * 0.52f;
        var tipWidth = leg.Width * 0.20f;
        DrawSegmentNoLock(
            canvas,
            SpiderLimbGeometry.Create(
                leg.Hip,
                leg.Knee,
                hipWidth,
                kneeWidth),
            palette,
            detailLevel);
        DrawSegmentNoLock(
            canvas,
            SpiderLimbGeometry.Create(
                leg.Knee,
                leg.Tip,
                kneeWidth,
                tipWidth),
            palette,
            detailLevel);
        DrawJointNoLock(
            canvas,
            leg.Hip,
            BisectorAngle(leg.Root, leg.Hip, leg.Knee),
            hipWidth,
            palette,
            detailLevel);
        DrawJointNoLock(
            canvas,
            leg.Knee,
            BisectorAngle(leg.Hip, leg.Knee, leg.Tip),
            kneeWidth,
            palette,
            detailLevel);
    }

    private void DrawSegmentNoLock(
        SKCanvas canvas,
        SpiderLimbSegment segment,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        if (!TryGetSegmentDrawingData(segment, out var data))
        {
            return;
        }

        _segmentPathBuilder.Reset();
        _segmentPathBuilder.MoveTo(segment.StartSideA);
        _segmentPathBuilder.QuadTo(
            data.ControlA.X,
            data.ControlA.Y,
            segment.EndSideA.X,
            segment.EndSideA.Y);
        _segmentPathBuilder.LineTo(segment.EndSideB);
        _segmentPathBuilder.QuadTo(
            data.ControlB.X,
            data.ControlB.Y,
            segment.StartSideB.X,
            segment.StartSideB.Y);
        _segmentPathBuilder.Close();
        using var path = _segmentPathBuilder.Detach();

        if (detailLevel == SpiderDetailLevel.Compact)
        {
            _segmentFillPaint.Shader = null;
            _segmentFillPaint.Color = palette.LegSurface;
            canvas.DrawPath(path, _segmentFillPaint);
        }
        else
        {
            SetGradientColors(palette);
            using var shader = SKShader.CreateLinearGradient(
                data.GradientStart,
                data.GradientEnd,
                _gradientColors,
                SegmentGradientStops,
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
        }

        _edgePaint.Color = CreateMutedEdge(palette.LegShadow);
        _edgePaint.StrokeWidth = data.EdgeWidth;
        canvas.DrawPath(path, _edgePaint);

        if (detailLevel >= SpiderDetailLevel.Standard)
        {
            _segmentHighlightPaint.Color = palette.BodyHighlight.WithAlpha(150);
            _segmentHighlightPaint.StrokeWidth = data.HighlightWidth;
            canvas.DrawLine(
                data.UseSideAForLight ? segment.StartSideA : segment.StartSideB,
                data.UseSideAForLight ? segment.EndSideA : segment.EndSideB,
                _segmentHighlightPaint);
        }

        if (detailLevel == SpiderDetailLevel.Showcase)
        {
            _showcasePaint.Color = palette.BodyHighlight.WithAlpha(68);
            _showcasePaint.StrokeWidth = data.ShowcaseWidth;
            DrawFacetLineNoLock(canvas, segment, 0.58f);
            DrawFacetLineNoLock(canvas, segment, 0.76f);
        }
    }

    private void DrawJointNoLock(
        SKCanvas canvas,
        SKPoint center,
        float angleRadians,
        float width,
        SpiderMaterialPalette palette,
        SpiderDetailLevel detailLevel)
    {
        if (!TryGetJointDrawingData(center, angleRadians, width, out var data))
        {
            return;
        }

        _jointPathBuilder.Reset();
        _jointPathBuilder.AddOval(data.LocalBounds, SKPathDirection.Clockwise);
        using var path = _jointPathBuilder.Detach();
        if (Math.Abs(data.AxisAngleRadians) > float.Epsilon)
        {
            var rotation = SKMatrix.CreateRotation(
                data.AxisAngleRadians,
                center.X,
                center.Y);
            path.Transform(in rotation);
        }

        if (detailLevel == SpiderDetailLevel.Compact)
        {
            _jointFillPaint.Shader = null;
            _jointFillPaint.Color = palette.LegSurface;
            canvas.DrawPath(path, _jointFillPaint);
            _edgePaint.Color = CreateMutedEdge(palette.LegShadow);
            _edgePaint.StrokeWidth = Math.Max(0.8f, width * 0.10f);
            canvas.DrawPath(path, _edgePaint);
            return;
        }

        SetGradientColors(palette);
        using var shader = SKShader.CreateRadialGradient(
            data.WorldLightFocus,
            data.ShaderRadius,
            _gradientColors,
            JointGradientStops,
            SKShaderTileMode.Clamp);
        try
        {
            _jointFillPaint.Shader = shader;
            canvas.DrawPath(path, _jointFillPaint);
        }
        finally
        {
            _jointFillPaint.Shader = null;
        }

        _jointHighlightPaint.Color = palette.BodyHighlight.WithAlpha(120);
        _jointHighlightPaint.StrokeWidth = data.HighlightWidth;
        canvas.Save();
        try
        {
            canvas.ClipPath(path);
            canvas.DrawArc(path.Bounds, 190, 160, false, _jointHighlightPaint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawFacetLineNoLock(
        SKCanvas canvas,
        SpiderLimbSegment segment,
        float amount)
    {
        if (!TryLerp(segment.StartSideA, segment.EndSideA, amount, out var sideA)
            || !TryLerp(segment.StartSideB, segment.EndSideB, amount, out var sideB)
            || !TryLerp(sideA, sideB, 0.20f, out var insetA)
            || !TryLerp(sideA, sideB, 0.80f, out var insetB))
        {
            return;
        }

        canvas.DrawLine(insetA, insetB, _showcasePaint);
    }

    private void SetGradientColors(SpiderMaterialPalette palette)
    {
        _gradientColors[0] = palette.BodyHighlight;
        _gradientColors[1] = palette.LegSurface;
        _gradientColors[2] = palette.LegShadow;
    }

    private static bool TryGetSegmentDrawingData(
        SpiderLimbSegment segment,
        out SegmentDrawingData data)
    {
        data = default;
        if (!segment.IsValid
            || !IsFinite(segment.Start)
            || !IsFinite(segment.End)
            || !IsFinite(segment.StartSideA)
            || !IsFinite(segment.StartSideB)
            || !IsFinite(segment.EndSideA)
            || !IsFinite(segment.EndSideB)
            || !float.IsFinite(segment.StartWidth)
            || !float.IsFinite(segment.EndWidth)
            || !float.IsFinite(segment.AngleRadians)
            || segment.StartWidth <= 0
            || segment.EndWidth <= 0)
        {
            return false;
        }

        var deltaX = (double)segment.End.X - segment.Start.X;
        var deltaY = (double)segment.End.Y - segment.Start.Y;
        var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (!double.IsFinite(length) || length <= MinimumVectorLength)
        {
            return false;
        }

        var normalX = deltaY / length;
        var normalY = -deltaX / length;
        var maximumWidth = Math.Max(segment.StartWidth, segment.EndWidth);
        var curveOffset = (double)maximumWidth * CurveOffsetRatio;
        var controlAX = ((double)segment.StartSideA.X + segment.EndSideA.X) / 2
            + normalX * curveOffset;
        var controlAY = ((double)segment.StartSideA.Y + segment.EndSideA.Y) / 2
            + normalY * curveOffset;
        var controlBX = ((double)segment.StartSideB.X + segment.EndSideB.X) / 2
            - normalX * curveOffset;
        var controlBY = ((double)segment.StartSideB.Y + segment.EndSideB.Y) / 2
            - normalY * curveOffset;
        var gradientStartX = Math.Min(segment.Start.X, segment.End.X) - (double)maximumWidth;
        var gradientStartY = Math.Min(segment.Start.Y, segment.End.Y) - (double)maximumWidth;
        var gradientEndX = Math.Max(segment.Start.X, segment.End.X) + (double)maximumWidth;
        var gradientEndY = Math.Max(segment.Start.Y, segment.End.Y) + (double)maximumWidth;
        var edgeWidth = Math.Max(0.8, maximumWidth * 0.10);
        var highlightWidth = Math.Max(0.7, maximumWidth * 0.08);
        var showcaseWidth = Math.Max(0.55, maximumWidth * 0.025);
        if (!TryPoint(controlAX, controlAY, out var controlA)
            || !TryPoint(controlBX, controlBY, out var controlB)
            || !TryPoint(gradientStartX, gradientStartY, out var gradientStart)
            || !TryPoint(gradientEndX, gradientEndY, out var gradientEnd)
            || !TryFloat(edgeWidth, out var safeEdgeWidth)
            || !TryFloat(highlightWidth, out var safeHighlightWidth)
            || !TryFloat(showcaseWidth, out var safeShowcaseWidth))
        {
            return false;
        }

        var scoreA = (double)segment.StartSideA.X + segment.StartSideA.Y
            + segment.EndSideA.X + segment.EndSideA.Y;
        var scoreB = (double)segment.StartSideB.X + segment.StartSideB.Y
            + segment.EndSideB.X + segment.EndSideB.Y;
        if (!double.IsFinite(scoreA) || !double.IsFinite(scoreB))
        {
            return false;
        }

        data = new SegmentDrawingData(
            controlA,
            controlB,
            gradientStart,
            gradientEnd,
            safeEdgeWidth,
            safeHighlightWidth,
            safeShowcaseWidth,
            scoreA <= scoreB);
        return true;
    }

    private static bool TryGetJointDrawingData(
        SKPoint center,
        float angleRadians,
        float width,
        out JointDrawingData data)
    {
        data = default;
        if (!IsFinite(center)
            || !float.IsFinite(angleRadians)
            || !float.IsFinite(width)
            || width <= 0)
        {
            return false;
        }

        var ellipseWidth = (double)width * 1.05;
        var ellipseHeight = (double)width * 0.78;
        var left = center.X - ellipseWidth / 2;
        var top = center.Y - ellipseHeight / 2;
        var right = center.X + ellipseWidth / 2;
        var bottom = center.Y + ellipseHeight / 2;
        var focusX = center.X - (double)width * 0.14;
        var focusY = center.Y - (double)width * 0.18;
        var shaderRadius = (double)width * 0.72;
        var highlightWidth = Math.Max(0.7, width * 0.07);
        if (!TryFloat(left, out var safeLeft)
            || !TryFloat(top, out var safeTop)
            || !TryFloat(right, out var safeRight)
            || !TryFloat(bottom, out var safeBottom)
            || !TryPoint(focusX, focusY, out var worldLightFocus)
            || !TryFloat(shaderRadius, out var safeShaderRadius)
            || safeShaderRadius <= 0
            || !TryFloat(highlightWidth, out var safeHighlightWidth)
            || safeRight <= safeLeft
            || safeBottom <= safeTop)
        {
            return false;
        }

        var bounds = new SKRect(safeLeft, safeTop, safeRight, safeBottom);
        if (!IsFinite(bounds))
        {
            return false;
        }

        data = new JointDrawingData(
            bounds,
            worldLightFocus,
            safeShaderRadius,
            safeHighlightWidth,
            NormalizeAxisAngle(angleRadians));
        return float.IsFinite(data.AxisAngleRadians);
    }

    private static float BisectorAngle(
        SKPoint previous,
        SKPoint center,
        SKPoint next)
    {
        if (!TryUnit(
                (double)center.X - previous.X,
                (double)center.Y - previous.Y,
                out var incomingX,
                out var incomingY)
            || !TryUnit(
                (double)next.X - center.X,
                (double)next.Y - center.Y,
                out var outgoingX,
                out var outgoingY))
        {
            return float.NaN;
        }

        var sumX = incomingX + outgoingX;
        var sumY = incomingY + outgoingY;
        var sumLengthSquared = sumX * sumX + sumY * sumY;
        return (float)Math.Atan2(
            sumLengthSquared <= 1e-12 ? outgoingY : sumY,
            sumLengthSquared <= 1e-12 ? outgoingX : sumX);
    }

    private static bool TryUnit(
        double x,
        double y,
        out double unitX,
        out double unitY)
    {
        unitX = 0;
        unitY = 0;
        var length = Math.Sqrt(x * x + y * y);
        if (!double.IsFinite(length) || length <= MinimumVectorLength)
        {
            return false;
        }

        unitX = x / length;
        unitY = y / length;
        return double.IsFinite(unitX) && double.IsFinite(unitY);
    }

    private static float NormalizeAxisAngle(float angleRadians)
    {
        var normalized = angleRadians % MathF.PI;
        if (normalized >= MathF.PI / 2)
        {
            normalized -= MathF.PI;
        }
        else if (normalized < -MathF.PI / 2)
        {
            normalized += MathF.PI;
        }

        return normalized;
    }

    private static bool TryLerp(
        SKPoint start,
        SKPoint end,
        float amount,
        out SKPoint point) =>
        TryPoint(
            start.X + ((double)end.X - start.X) * amount,
            start.Y + ((double)end.Y - start.Y) * amount,
            out point);

    private static bool TryPoint(double x, double y, out SKPoint point)
    {
        point = default;
        if (!TryFloat(x, out var safeX) || !TryFloat(y, out var safeY))
        {
            return false;
        }

        point = new SKPoint(safeX, safeY);
        return true;
    }

    private static bool TryFloat(double value, out float result)
    {
        result = default;
        if (!double.IsFinite(value)
            || value < -float.MaxValue
            || value > float.MaxValue)
        {
            return false;
        }

        result = (float)value;
        return float.IsFinite(result);
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

    private static bool IsFinite(SKPoint point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y);

    private static bool IsFinite(SKRect rectangle) =>
        float.IsFinite(rectangle.Left)
        && float.IsFinite(rectangle.Top)
        && float.IsFinite(rectangle.Right)
        && float.IsFinite(rectangle.Bottom);

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private readonly record struct SegmentDrawingData(
        SKPoint ControlA,
        SKPoint ControlB,
        SKPoint GradientStart,
        SKPoint GradientEnd,
        float EdgeWidth,
        float HighlightWidth,
        float ShowcaseWidth,
        bool UseSideAForLight);

    private readonly record struct JointDrawingData(
        SKRect LocalBounds,
        SKPoint WorldLightFocus,
        float ShaderRadius,
        float HighlightWidth,
        float AxisAngleRadians);
}
