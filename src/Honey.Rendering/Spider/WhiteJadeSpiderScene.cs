using Honey.Domain.Model;
using Honey.Rendering.Effects;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed class WhiteJadeSpiderScene : IRenderScene, IDisposable
{
    private readonly WebScene _webScene = new();
    private readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private readonly SKPaint _legShadowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };
    private readonly SKPaint _legJadePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };
    private readonly SKPaint _veinPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };
    private readonly SKPaint _facetPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };
    private readonly SKPaint _bodyPaint = new() { IsAntialias = true };
    private readonly SKPaint _socketPaint = new() { IsAntialias = true };
    private readonly SKPaint _irisPaint = new() { IsAntialias = true };
    private int _disposed;

    public void Draw(SKCanvas canvas, RenderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        var clip = canvas.DeviceClipBounds;
        if (clip.Width <= 0 || clip.Height <= 0)
        {
            return;
        }

        Draw(canvas, snapshot, clip.Width, clip.Height, 1, clip.Left, clip.Top);
    }

    public void Draw(
        SKCanvas canvas,
        RenderSnapshot snapshot,
        float viewportWidth,
        float viewportHeight,
        float deviceScale = 1) =>
        Draw(canvas, snapshot, viewportWidth, viewportHeight, deviceScale, 0, 0);

    private void Draw(
        SKCanvas canvas,
        RenderSnapshot snapshot,
        float viewportWidth,
        float viewportHeight,
        float deviceScale,
        float offsetX,
        float offsetY)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        var safe = (snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Normalize();
        if (!float.IsFinite(viewportWidth)
            || !float.IsFinite(viewportHeight)
            || viewportWidth <= 0
            || viewportHeight <= 0)
        {
            return;
        }

        var bounds = new SKRect(0, 0, viewportWidth, viewportHeight);
        var pose = SpiderGeometry.CreatePose(
            bounds.Width,
            bounds.Height,
            safe,
            deviceScale);
        DrawPose(canvas, safe, pose, bounds, offsetX, offsetY);
    }

    public void Draw(SKCanvas canvas, RenderSnapshot snapshot, SpiderPose pose)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(pose);
        var safe = snapshot.Normalize();
        var bounds = new SKRect(0, 0, pose.ViewportWidth, pose.ViewportHeight);
        DrawPose(canvas, safe, pose, bounds, 0, 0);
    }

    private void DrawPose(
        SKCanvas canvas,
        RenderSnapshot safe,
        SpiderPose pose,
        SKRect bounds,
        float offsetX,
        float offsetY)
    {
        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        DrawParticles(canvas, pose, safe);
        _webScene.Draw(canvas, new SKRect(0, 0, bounds.Width, bounds.Height), safe);
        DrawLegs(canvas, pose, safe);
        DrawBody(canvas, pose, safe);
        DrawEyes(canvas, pose, safe);
        canvas.Restore();
    }

    private void DrawParticles(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var berserk = snapshot.Mode == PetMode.Berserk;
        var baseColor = berserk
            ? new SKColor(240, 48, 44, 125)
            : new SKColor(165, 246, 234, 95);
        var span = pose.Abdomen.Height * 1.1f;
        for (var index = 0; index < 14; index++)
        {
            var seed = index * 12.9898;
            var phase = snapshot.AnimationTime * (berserk ? 1.9 : 0.65) + seed;
            var x = pose.Center.X + MathF.Sin((float)(seed * 0.73)) * span;
            var y = pose.Center.Y + MathF.Cos((float)(seed * 0.41 + phase)) * span * 0.8f;
            var pulse = 0.55f + 0.45f * MathF.Sin((float)phase);
            _particlePaint.Color =
                baseColor.WithAlpha((byte)(baseColor.Alpha * Math.Clamp(pulse, 0.15f, 1)));
            canvas.DrawCircle(
                x,
                y,
                (1.2f + index % 3 * 0.45f) * pose.DeviceScale,
                _particlePaint);
        }
    }

    private void DrawLegs(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var berserk = snapshot.Mode == PetMode.Berserk;
        _legShadowPaint.Color =
            berserk ? new SKColor(122, 14, 18, 150) : new SKColor(28, 86, 80, 115);
        _legJadePaint.Color =
            berserk ? new SKColor(237, 111, 103, 235) : new SKColor(198, 242, 231, 235);

        foreach (var leg in pose.Legs)
        {
            _legShadowPaint.StrokeWidth = leg.Width * 1.18f;
            _legJadePaint.StrokeWidth = leg.Width * 0.72f;
            canvas.DrawLine(leg.Root, leg.Knee, _legShadowPaint);
            canvas.DrawLine(leg.Knee, leg.Tip, _legShadowPaint);
            canvas.DrawLine(leg.Root, leg.Knee, _legJadePaint);
            canvas.DrawLine(leg.Knee, leg.Tip, _legJadePaint);
            canvas.DrawCircle(leg.Knee, leg.Width * 0.43f, _legJadePaint);
        }
    }

    private void DrawBody(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var berserk = snapshot.Mode == PetMode.Berserk;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)(snapshot.AnimationTime * 4.2));
        var edge = berserk ? new SKColor(93, 9, 15, 245) : new SKColor(51, 115, 107, 230);
        var middle = berserk
            ? new SKColor(207, (byte)(48 + pulse * 35), 52, 245)
            : new SKColor(186, 235, 223, 245);
        var highlight = berserk ? new SKColor(255, 192, 166, 245) : new SKColor(246, 255, 250, 250);
        DrawJadeEllipse(canvas, pose.Abdomen, edge, middle, highlight);
        DrawJadeEllipse(canvas, pose.Head, edge, middle, highlight);

        _veinPaint.StrokeWidth = Math.Max(1, pose.Abdomen.Width * 0.025f);
        _veinPaint.Color = berserk
            ? new SKColor(255, 72, 58, (byte)(100 + pulse * 90))
            : new SKColor(90, 181, 165, 85);
        using var veinBuilder = new SKPathBuilder();
        veinBuilder.MoveTo(pose.Abdomen.MidX, pose.Abdomen.Top + pose.Abdomen.Height * 0.18f);
        veinBuilder.CubicTo(
            pose.Abdomen.Left + pose.Abdomen.Width * 0.28f,
            pose.Abdomen.MidY,
            pose.Abdomen.Right - pose.Abdomen.Width * 0.2f,
            pose.Abdomen.MidY,
            pose.Abdomen.MidX,
            pose.Abdomen.Bottom - pose.Abdomen.Height * 0.15f);
        using var veins = veinBuilder.Detach();
        canvas.DrawPath(veins, _veinPaint);
    }

    private void DrawJadeEllipse(
        SKCanvas canvas,
        SKRect rectangle,
        SKColor edge,
        SKColor middle,
        SKColor highlight)
    {
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(rectangle.Left + rectangle.Width * 0.36f, rectangle.Top + rectangle.Height * 0.28f),
            Math.Max(rectangle.Width, rectangle.Height) * 0.76f,
            [highlight, middle, edge],
            [0, 0.5f, 1],
            SKShaderTileMode.Clamp);
        try
        {
            _bodyPaint.Shader = shader;
            canvas.DrawOval(rectangle, _bodyPaint);
        }
        finally
        {
            _bodyPaint.Shader = null;
        }

        _facetPaint.StrokeWidth = Math.Max(1, rectangle.Width * 0.025f);
        _facetPaint.Color = highlight.WithAlpha(115);
        canvas.DrawArc(rectangle, 205, 82, false, _facetPaint);
    }

    private void DrawEyes(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var berserk = snapshot.Mode == PetMode.Berserk;
        var sleepy = snapshot.Mood == PetMood.Sleepy;
        var alert = snapshot.Mood is PetMood.Alert or PetMood.Angry;
        var eyeColor = berserk || snapshot.Mood == PetMood.Angry
            ? new SKColor(255, 38, 34, 255)
            : snapshot.Mood == PetMood.Hungry
                ? new SKColor(218, 184, 78, 245)
                : new SKColor(44, 103, 97, 245);
        var unit = pose.Head.Width;
        var gazeX = Math.Clamp(snapshot.LookX, -1, 1) * unit * 0.035f;
        var gazeY = Math.Clamp(snapshot.LookY, -1, 1) * unit * 0.025f;
        _socketPaint.Color = new SKColor(21, 40, 38, 220);
        _irisPaint.Color = eyeColor;
        for (var row = 0; row < 2; row++)
        {
            for (var pair = 0; pair < 2; pair++)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var x = pose.Head.MidX + side * unit * (pair == 0 ? 0.14f : 0.31f);
                    var y = pose.Head.Top + pose.Head.Height * (row == 0 ? 0.42f : 0.67f);
                    var radius = unit * (pair == 0 ? 0.07f : 0.045f) * (alert ? 1.14f : 1);
                    canvas.DrawOval(
                        new SKRect(x - radius, y - radius * (sleepy ? 0.28f : 1), x + radius, y + radius * (sleepy ? 0.28f : 1)),
                        _socketPaint);
                    canvas.DrawCircle(
                        x + gazeX,
                        y + gazeY,
                        radius * (sleepy ? 0.22f : 0.58f),
                        _irisPaint);
                }
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _webScene.Dispose();
        _particlePaint.Dispose();
        _legShadowPaint.Dispose();
        _legJadePaint.Dispose();
        _veinPaint.Dispose();
        _facetPaint.Dispose();
        _bodyPaint.Dispose();
        _socketPaint.Dispose();
        _irisPaint.Dispose();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
