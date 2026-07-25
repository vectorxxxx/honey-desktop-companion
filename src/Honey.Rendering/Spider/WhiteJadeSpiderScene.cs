using Honey.Domain.Model;
using Honey.Domain.Behavior;
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
    private readonly SKPaint _skillPaint = new()
    {
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };
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
        DrawSkillProps(canvas, pose, safe);
        DrawParticles(canvas, pose, safe);
        _webScene.Draw(canvas, new SKRect(0, 0, bounds.Width, bounds.Height), safe);
        DrawLegs(canvas, pose, safe);
        DrawBody(canvas, pose, safe);
        DrawEyes(canvas, pose, safe);
        canvas.Restore();
    }

    private void DrawSkillProps(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var jade = snapshot.Mode == PetMode.Berserk
            ? new SKColor(244, 67, 64, 210)
            : new SKColor(176, 248, 234, 210);
        _skillPaint.Color = jade;
        _skillPaint.StrokeWidth = Math.Max(1.5f, pose.DeviceScale * 2);
        _skillPaint.Style = SKPaintStyle.Stroke;
        var progress = (float)snapshot.PhaseProgress;
        switch (snapshot.Behavior)
        {
            case BuiltInBehaviorKeys.Forage:
            {
                var approach = snapshot.Phase == BuiltInPhaseKeys.ForageApproach;
                var capture = snapshot.Phase == BuiltInPhaseKeys.ForageCapture;
                var eat = snapshot.Phase == BuiltInPhaseKeys.ForageEat;
                var x = eat
                    ? pose.Head.MidX
                    : pose.Center.X + pose.Abdomen.Width
                        * (approach ? 0.9f - progress * 0.42f : 0.9f);
                var y = eat
                    ? pose.Head.MidY
                    : pose.Center.Y - pose.Abdomen.Height
                        * (0.65f + MathF.Sin(progress * MathF.PI) * 0.2f);
                if (capture)
                {
                    canvas.DrawCircle(x, y, (5 + progress * 13) * pose.DeviceScale, _skillPaint);
                    canvas.DrawLine(pose.Head.MidX, pose.Head.MidY, x, y, _skillPaint);
                }
                else if (eat)
                {
                    _skillPaint.Style = SKPaintStyle.Fill;
                    _skillPaint.Color = jade.WithAlpha((byte)(80 + 150 * progress));
                    canvas.DrawCircle(x, y, (4 + progress * 5) * pose.DeviceScale, _skillPaint);
                    _skillPaint.Style = SKPaintStyle.Stroke;
                }
                else
                {
                    DrawButterfly(canvas, x, y, pose.DeviceScale);
                    if (approach)
                    {
                        canvas.DrawLine(pose.Head.MidX, pose.Head.MidY, x, y, _skillPaint);
                    }
                }
                break;
            }
            case BuiltInBehaviorKeys.Web:
                if (snapshot.Phase == BuiltInPhaseKeys.WebAnchor)
                {
                    canvas.DrawCircle(pose.Abdomen.Left, pose.Abdomen.Top, 5 * pose.DeviceScale, _skillPaint);
                    canvas.DrawCircle(pose.Abdomen.Right, pose.Abdomen.Top, 5 * pose.DeviceScale, _skillPaint);
                }
                else if (snapshot.Phase == BuiltInPhaseKeys.WebRest)
                {
                    canvas.DrawLine(
                        pose.Center.X,
                        pose.Abdomen.Top - pose.Abdomen.Height * 0.8f,
                        pose.Center.X,
                        pose.Abdomen.Top,
                        _skillPaint);
                }
                break;
            case BuiltInBehaviorKeys.Play:
            {
                var chase = snapshot.Phase == BuiltInPhaseKeys.PlayChase;
                var bounce = chase ? 0 : MathF.Sin(progress * MathF.PI);
                var x = pose.Center.X + pose.Abdomen.Width
                    * (chase ? -0.75f + progress * 1.5f : 0.62f);
                var y = pose.Center.Y + pose.Abdomen.Height
                    * (chase ? 0.55f : 0.45f - bounce * 0.75f);
                _skillPaint.Style = SKPaintStyle.Fill;
                canvas.DrawCircle(x, y, 8 * pose.DeviceScale, _skillPaint);
                _skillPaint.Style = SKPaintStyle.Stroke;
                if (chase)
                {
                    canvas.DrawLine(x - 28 * pose.DeviceScale, y, x - 12 * pose.DeviceScale, y, _skillPaint);
                }
                else
                {
                    canvas.DrawArc(
                        new SKRect(x - 11, y - 11, x + 11, y + 11),
                        20, 230, false, _skillPaint);
                }
                break;
            }
            case BuiltInBehaviorKeys.Observe:
                if (snapshot.Phase == BuiltInPhaseKeys.ObserveTurn)
                {
                    canvas.DrawArc(
                        new SKRect(
                            pose.Head.Left - 12,
                            pose.Head.Top - 12,
                            pose.Head.Right + 12,
                            pose.Head.Bottom + 12),
                        200,
                        100 * progress,
                        false,
                        _skillPaint);
                }
                else
                {
                    var targetX = pose.Center.X + snapshot.LookX * pose.Abdomen.Width;
                    var targetY = pose.Center.Y + snapshot.LookY * pose.Abdomen.Height;
                    canvas.DrawLine(pose.Head.MidX, pose.Head.MidY, targetX, targetY, _skillPaint);
                    canvas.DrawCircle(targetX, targetY, 4 * pose.DeviceScale, _skillPaint);
                }
                break;
            case BuiltInBehaviorKeys.Pounce:
            {
                var charge = snapshot.Phase == BuiltInPhaseKeys.PounceCharge;
                var retreat = snapshot.Phase == BuiltInPhaseKeys.PounceRetreat;
                if (charge)
                {
                    canvas.DrawOval(
                        new SKRect(
                            pose.Abdomen.Left - progress * 14,
                            pose.Abdomen.Top + progress * 8,
                            pose.Abdomen.Right + progress * 14,
                            pose.Abdomen.Bottom - progress * 8),
                        _skillPaint);
                    break;
                }

                var length = pose.Abdomen.Width * (0.25f + progress * 0.45f);
                var origin = retreat ? pose.Abdomen.Right : pose.Abdomen.Left;
                var direction = retreat ? 1 : -1;
                for (var index = -1; index <= 1; index++)
                {
                    var y = pose.Center.Y + index * 9 * pose.DeviceScale;
                    canvas.DrawLine(
                        origin + direction * length,
                        y,
                        origin + direction * length * 0.25f,
                        y,
                        _skillPaint);
                }
                break;
            }
            case BuiltInBehaviorKeys.Groom:
                var finish = snapshot.Phase == BuiltInPhaseKeys.GroomFinish;
                var alternate = snapshot.Phase == BuiltInPhaseKeys.GroomAlternate;
                var offset = alternate ? MathF.Sin(progress * MathF.PI * 4) * 38 : finish ? 55 : 0;
                canvas.DrawArc(pose.Head, 170 + offset, finish ? 24 : 58, false, _skillPaint);
                canvas.DrawArc(pose.Head, 312 - offset, finish ? 24 : 58, false, _skillPaint);
                break;
            case BuiltInBehaviorKeys.Sleep:
            {
                var breathe = snapshot.Phase == BuiltInPhaseKeys.SleepBreathe;
                _skillPaint.Color = jade.WithAlpha((byte)(80 + progress * 110));
                var inset = breathe ? -12 - progress * 5 : 4 + progress * 13;
                canvas.DrawOval(
                    new SKRect(
                        pose.Abdomen.Left + inset,
                        pose.Abdomen.Top + inset,
                        pose.Abdomen.Right - inset,
                        pose.Abdomen.Bottom - inset),
                    _skillPaint);
                break;
            }
        }
    }

    private void DrawButterfly(SKCanvas canvas, float x, float y, float scale)
    {
        canvas.DrawLine(x, y, x - 9 * scale, y - 7 * scale, _skillPaint);
        canvas.DrawLine(x, y, x + 9 * scale, y - 7 * scale, _skillPaint);
        canvas.DrawCircle(x, y, 2.2f * scale, _skillPaint);
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
        _skillPaint.Dispose();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
