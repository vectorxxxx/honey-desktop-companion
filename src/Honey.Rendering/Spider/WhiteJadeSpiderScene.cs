using Honey.Domain.Model;
using Honey.Domain.Behavior;
using Honey.Rendering.Effects;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed class WhiteJadeSpiderScene : IRenderScene, IDisposable
{
    private readonly WebScene _webScene = new();
    private readonly ISpiderBodyAtlas? _atlas;
    private readonly bool _ownsAtlas;
    private readonly SpiderDirectionSelector _directionSelector = new();
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
    private readonly SKPaint _legHighlightPaint = new()
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
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKPaint _socketPaint = new() { IsAntialias = true };
    private readonly SKPaint _irisPaint = new() { IsAntialias = true };
    private readonly SKPaint _skillPaint = new()
    {
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };
    private int _disposed;

    public WhiteJadeSpiderScene()
    {
        if (EmbeddedSpiderBodyAtlas.TryLoadDefault(out var atlas))
        {
            _atlas = atlas;
            _ownsAtlas = true;
        }
    }

    public WhiteJadeSpiderScene(ISpiderBodyAtlas atlas)
        : this(atlas, ownsAtlas: true)
    {
    }

    public WhiteJadeSpiderScene(ISpiderBodyAtlas atlas, bool ownsAtlas)
    {
        _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        _ownsAtlas = ownsAtlas;
    }

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
        var detailLevel = SpiderDetailLevelSelector.Select(
            MathF.Min(bounds.Width, bounds.Height) * safe.Scale);
        DrawPose(canvas, safe, pose, bounds, detailLevel, offsetX, offsetY);
    }

    public void Draw(SKCanvas canvas, RenderSnapshot snapshot, SpiderPose pose)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(pose);
        var safe = snapshot.Normalize();
        var bounds = new SKRect(0, 0, pose.ViewportWidth, pose.ViewportHeight);
        var detailLevel = SpiderDetailLevelSelector.Select(
            MathF.Min(bounds.Width, bounds.Height) * safe.Scale);
        DrawPose(canvas, safe, pose, bounds, detailLevel, 0, 0);
    }

    private void DrawPose(
        SKCanvas canvas,
        RenderSnapshot safe,
        SpiderPose pose,
        SKRect bounds,
        SpiderDetailLevel detailLevel,
        float offsetX,
        float offsetY)
    {
        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        DrawSkillProps(canvas, pose, safe);
        if (detailLevel >= SpiderDetailLevel.Standard)
        {
            DrawParticles(canvas, pose, safe, detailLevel);
        }
        _webScene.Draw(canvas, new SKRect(0, 0, bounds.Width, bounds.Height), safe);
        DrawLegs(canvas, pose, safe, detailLevel, drawFront: false);
        var direction = _directionSelector.Select(safe.FacingX, safe.FacingY);
        var atlasDrawn = false;
        if (_atlas is not null
            && _atlas.TryGetFrame(safe.Mode, direction, out var frame))
        {
            DrawAtlasBody(canvas, pose, frame, safe, detailLevel);
            atlasDrawn = true;
        }
        else
        {
            DrawProceduralBody(canvas, pose, safe);
        }

        DrawLegs(canvas, pose, safe, detailLevel, drawFront: true);
        if (!atlasDrawn)
        {
            DrawMouthparts(canvas, pose, safe);
            DrawEyes(canvas, pose, safe);
        }
        else if (detailLevel == SpiderDetailLevel.Showcase)
        {
            DrawShowcaseAccents(canvas, pose, safe);
        }
        canvas.Restore();
    }

    private void DrawSkillProps(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var jade = SpiderMaterialPalette.For(snapshot.Mode).LegSurface.WithAlpha(210);
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

    private void DrawParticles(
        SKCanvas canvas,
        SpiderPose pose,
        RenderSnapshot snapshot,
        SpiderDetailLevel detailLevel)
    {
        var berserk = snapshot.Mode == PetMode.Berserk;
        var baseColor = SpiderMaterialPalette.For(snapshot.Mode).Particle;
        var span = pose.Abdomen.Height * 1.1f;
        var particleCount = detailLevel == SpiderDetailLevel.Showcase ? 14 : 7;
        for (var index = 0; index < particleCount; index++)
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

    private void DrawLegs(
        SKCanvas canvas,
        SpiderPose pose,
        RenderSnapshot snapshot,
        SpiderDetailLevel detailLevel,
        bool drawFront)
    {
        var palette = SpiderMaterialPalette.For(snapshot.Mode);
        _legShadowPaint.Color = palette.LegShadow;
        _legJadePaint.Color = palette.LegSurface;
        _legHighlightPaint.Color = palette.BodyHighlight.WithAlpha(190);
        var forward = UnitForward(snapshot);

        foreach (var leg in pose.Legs)
        {
            var rootDelta = leg.Root - pose.Center;
            var isFront = rootDelta.X * forward.X + rootDelta.Y * forward.Y > 0;
            if (isFront != drawFront)
            {
                continue;
            }

            DrawLegSegment(
                canvas,
                leg.Root,
                leg.Knee,
                leg.Width * 1.16f,
                detailLevel);
            DrawLegSegment(
                canvas,
                leg.Knee,
                leg.Tip,
                leg.Width * 0.82f,
                detailLevel);
            if (detailLevel >= SpiderDetailLevel.Standard)
            {
                DrawJointCap(canvas, leg.Knee, leg.Width, palette);
            }
        }
    }

    private void DrawLegSegment(
        SKCanvas canvas,
        SKPoint start,
        SKPoint end,
        float width,
        SpiderDetailLevel detailLevel)
    {
        _legShadowPaint.StrokeWidth = width * 1.28f;
        _legJadePaint.StrokeWidth = width * 0.76f;
        _legHighlightPaint.StrokeWidth = Math.Max(0.8f, width * 0.17f);
        canvas.DrawLine(start, end, _legShadowPaint);
        canvas.DrawLine(start, end, _legJadePaint);
        if (detailLevel >= SpiderDetailLevel.Standard)
        {
            canvas.DrawLine(start, end, _legHighlightPaint);
        }
    }

    private void DrawJointCap(
        SKCanvas canvas,
        SKPoint center,
        float width,
        SpiderMaterialPalette palette)
    {
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(center.X - width * 0.14f, center.Y - width * 0.18f),
            width * 0.58f,
            [palette.BodyHighlight, palette.LegSurface, palette.LegShadow],
            [0, 0.55f, 1],
            SKShaderTileMode.Clamp);
        _bodyPaint.Shader = shader;
        canvas.DrawOval(
            SKRect.Create(
                center.X - width * 0.48f,
                center.Y - width * 0.40f,
                width * 0.96f,
                width * 0.80f),
            _bodyPaint);
        _bodyPaint.Shader = null;
    }

    private void DrawAtlasBody(
        SKCanvas canvas,
        SpiderPose pose,
        SpiderAtlasFrame frame,
        RenderSnapshot snapshot,
        SpiderDetailLevel detailLevel)
    {
        var bodySpan = MathF.Max(
            pose.Abdomen.Width * 1.42f,
            Distance(pose.Abdomen.Center, pose.Head.Center)
                + pose.Abdomen.RadiusY
                + pose.Head.RadiusY);
        var side = MathF.Min(
            MathF.Min(pose.ContentBounds.Width, pose.ContentBounds.Height) * 0.98f,
            bodySpan * 1.70f);
        var destination = SKRect.Create(
            pose.Center.X - side * frame.NormalizedAnchor.X,
            pose.Center.Y - side * frame.NormalizedAnchor.Y,
            side,
            side);
        var source = new SKRect(
            frame.Source.Left,
            frame.Source.Top,
            frame.Source.Right,
            frame.Source.Bottom);
        var sampling = new SKSamplingOptions(
            SKFilterMode.Linear,
            SKMipmapMode.Linear);
        canvas.Save();
        if (frame.FlipX)
        {
            canvas.Scale(-1, 1, destination.MidX, destination.MidY);
        }

        canvas.DrawBitmap(frame.Bitmap, source, destination, sampling, null);
        canvas.Restore();

        if (snapshot.Mode == PetMode.Berserk
            && detailLevel >= SpiderDetailLevel.Standard)
        {
            var pulse = 0.45f
                + 0.35f * MathF.Sin((float)(snapshot.AnimationTime * 4.2));
            _glowPaint.Color = new SKColor(
                255,
                34,
                26,
                (byte)(36 + pulse * 44));
            canvas.DrawOval(pose.Abdomen.Bounds, _glowPaint);
        }
    }

    private void DrawShowcaseAccents(
        SKCanvas canvas,
        SpiderPose pose,
        RenderSnapshot snapshot)
    {
        var palette = SpiderMaterialPalette.For(snapshot.Mode);
        _facetPaint.Color = palette.BodyHighlight.WithAlpha(115);
        _facetPaint.StrokeWidth = Math.Max(1, pose.DeviceScale * 1.2f);
        canvas.DrawArc(pose.Abdomen.Bounds, 205, 54, false, _facetPaint);
    }

    private void DrawProceduralBody(
        SKCanvas canvas,
        SpiderPose pose,
        RenderSnapshot snapshot)
    {
        var berserk = snapshot.Mode == PetMode.Berserk;
        var palette = SpiderMaterialPalette.For(snapshot.Mode);
        var pulse = 0.5f + 0.5f * MathF.Sin((float)(snapshot.AnimationTime * 4.2));
        DrawOrientedJadeEllipse(
            canvas,
            pose.Abdomen,
            palette.BodyEdge,
            berserk
                ? new SKColor(
                    (byte)Math.Min(255, palette.BodyMiddle.Red + pulse * 24),
                    palette.BodyMiddle.Green,
                    palette.BodyMiddle.Blue,
                    palette.BodyMiddle.Alpha)
                : palette.BodyMiddle,
            palette.BodyHighlight);
        DrawOrientedJadeEllipse(
            canvas,
            pose.Head,
            palette.BodyEdge,
            palette.BodyMiddle,
            palette.BodyHighlight);

        canvas.Save();
        canvas.RotateRadians(
            pose.Abdomen.RotationRadians,
            pose.Abdomen.Center.X,
            pose.Abdomen.Center.Y);
        var local = LocalBounds(pose.Abdomen);
        _glowPaint.Color = palette.InternalGlow.WithAlpha(
            (byte)Math.Clamp(
                palette.InternalGlow.Alpha * (berserk ? 0.65f + pulse * 0.35f : 1),
                0,
                255));
        canvas.DrawOval(
            SKRect.Create(
                local.Left + local.Width * 0.22f,
                local.Top + local.Height * 0.25f,
                local.Width * 0.56f,
                local.Height * 0.48f),
            _glowPaint);

        _veinPaint.StrokeWidth = Math.Max(1, pose.Abdomen.Width * 0.025f);
        _veinPaint.Color = berserk
            ? palette.Vein.WithAlpha((byte)(105 + pulse * 85))
            : palette.Vein;
        using var veinBuilder = new SKPathBuilder();
        veinBuilder.MoveTo(local.MidX, local.Top + local.Height * 0.18f);
        veinBuilder.CubicTo(
            local.Left + local.Width * 0.28f,
            local.MidY,
            local.Right - local.Width * 0.2f,
            local.MidY,
            local.MidX,
            local.Bottom - local.Height * 0.15f);
        using var veins = veinBuilder.Detach();
        canvas.DrawPath(veins, _veinPaint);
        canvas.Restore();
    }

    private void DrawMouthparts(SKCanvas canvas, SpiderPose pose, RenderSnapshot snapshot)
    {
        var palette = SpiderMaterialPalette.For(snapshot.Mode);
        var forward = UnitForward(snapshot);
        var side = new SKPoint(-forward.Y, forward.X);
        var rootDistance = pose.Head.Width * 0.24f;
        var fangLength = pose.Head.Width * 0.23f;
        _legShadowPaint.Color = palette.LegShadow;
        _legShadowPaint.StrokeWidth = Math.Max(2.2f * pose.DeviceScale, pose.Head.Width * 0.075f);
        _legJadePaint.Color = palette.LegSurface;
        _legJadePaint.StrokeWidth = _legShadowPaint.StrokeWidth * 0.48f;
        for (var direction = -1; direction <= 1; direction += 2)
        {
            var root = new SKPoint(
                pose.Head.MidX + forward.X * rootDistance + side.X * direction * pose.Head.Width * 0.13f,
                pose.Head.MidY + forward.Y * rootDistance + side.Y * direction * pose.Head.Width * 0.13f);
            var tip = new SKPoint(
                root.X + forward.X * fangLength - side.X * direction * pose.Head.Width * 0.06f,
                root.Y + forward.Y * fangLength - side.Y * direction * pose.Head.Width * 0.06f);
            canvas.DrawLine(root, tip, _legShadowPaint);
            canvas.DrawLine(root, tip, _legJadePaint);
        }
    }

    private void DrawOrientedJadeEllipse(
        SKCanvas canvas,
        OrientedEllipse part,
        SKColor edge,
        SKColor middle,
        SKColor highlight)
    {
        canvas.Save();
        canvas.RotateRadians(
            part.RotationRadians,
            part.Center.X,
            part.Center.Y);
        DrawJadeEllipse(
            canvas,
            LocalBounds(part),
            edge,
            middle,
            highlight);
        canvas.Restore();
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
        var palette = SpiderMaterialPalette.For(snapshot.Mode);
        var sleepy = snapshot.Mood == PetMood.Sleepy;
        var alert = snapshot.Mood is PetMood.Alert or PetMood.Angry;
        var eyeColor = berserk || snapshot.Mood == PetMood.Angry
            ? new SKColor(255, 38, 34, 255)
            : snapshot.Mood == PetMood.Hungry
                ? new SKColor(218, 184, 78, 245)
                : palette.Eye;
        var unit = pose.Head.Width;
        var gazeX = Math.Clamp(snapshot.LookX, -1, 1) * unit * 0.035f;
        var gazeY = Math.Clamp(snapshot.LookY, -1, 1) * unit * 0.025f;
        _socketPaint.Color = new SKColor(18, 22, 25, 232);
        _irisPaint.Color = eyeColor;
        for (var row = 0; row < 2; row++)
        {
            for (var pair = 0; pair < 2; pair++)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var center = TransformLocal(
                        pose.Head,
                        side * unit * (pair == 0 ? 0.14f : 0.31f),
                        -pose.Head.RadiusY
                            + pose.Head.Height * (row == 0 ? 0.42f : 0.67f));
                    var x = center.X;
                    var y = center.Y;
                    var radius = unit * (pair == 0 ? 0.07f : 0.045f) * (alert ? 1.14f : 1);
                    canvas.Save();
                    canvas.RotateRadians(pose.Head.RotationRadians, x, y);
                    canvas.DrawOval(new SKRect(
                        x - radius,
                        y - radius * (sleepy ? 0.28f : 1),
                        x + radius,
                        y + radius * (sleepy ? 0.28f : 1)),
                        _socketPaint);
                    canvas.DrawCircle(
                        x + gazeX,
                        y + gazeY,
                        radius * (sleepy ? 0.22f : 0.58f),
                        _irisPaint);
                    canvas.Restore();
                }
            }
        }
    }

    private static SKRect LocalBounds(OrientedEllipse part) =>
        SKRect.Create(
            part.Center.X - part.RadiusX,
            part.Center.Y - part.RadiusY,
            part.Width,
            part.Height);

    private static SKPoint TransformLocal(
        OrientedEllipse part,
        float localX,
        float localY)
    {
        var cosine = MathF.Cos(part.RotationRadians);
        var sine = MathF.Sin(part.RotationRadians);
        return new SKPoint(
            part.Center.X + localX * cosine - localY * sine,
            part.Center.Y + localX * sine + localY * cosine);
    }

    private static SKPoint UnitForward(RenderSnapshot snapshot)
    {
        var forward = new SKPoint(snapshot.FacingX, snapshot.FacingY);
        var length = MathF.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
        return length < 0.001f
            ? new SKPoint(0, -1)
            : new SKPoint(forward.X / length, forward.Y / length);
    }

    private static float Distance(SKPoint left, SKPoint right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        return MathF.Sqrt(x * x + y * y);
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
        _legHighlightPaint.Dispose();
        _veinPaint.Dispose();
        _facetPaint.Dispose();
        _bodyPaint.Dispose();
        _glowPaint.Dispose();
        _socketPaint.Dispose();
        _irisPaint.Dispose();
        _skillPaint.Dispose();
        if (_ownsAtlas)
        {
            _atlas?.Dispose();
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
