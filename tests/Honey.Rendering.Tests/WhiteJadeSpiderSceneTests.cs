using Honey.Domain.Model;
using Honey.Domain.Behavior;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class WhiteJadeSpiderSceneTests
{
    [Fact]
    public void Scene_玉足由独立渲染器接管且移除旧直线画笔()
    {
        var source = ReadSceneSource();

        Assert.Contains("SpiderLimbRenderer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLegSegment(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_legJadePaint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_legHighlightPaint", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Draw_前景腿根被身体遮挡而外段覆盖身体()
    {
        var bodyColor = new SKColor(13, 233, 71, 255);
        using var atlas = new SolidBodyAtlas(bodyColor);
        using var scene = new WhiteJadeSpiderScene(atlas, ownsAtlas: false);
        var snapshot = Snapshot(PetMode.Normal) with
        {
            FacingX = 0,
            FacingY = -1
        };
        var pose = SpiderGeometry.CreatePose(256, 256, snapshot);
        var frontLeg = pose.Legs.First(
            leg => leg.Layer == SpiderLegLayer.AboveBody
                && leg.Root.X < pose.Center.X
                && leg.Root.Y < pose.Center.Y);

        using var bitmap = Render(scene, snapshot);

        Assert.Equal(bodyColor, PixelAtMidpoint(bitmap, frontLeg.Root, frontLeg.Hip));
        Assert.NotEqual(bodyColor, PixelAtMidpoint(bitmap, frontLeg.Hip, frontLeg.Knee));
    }

    [Fact]
    public void Draw_普通与狂暴有明显像素差异且四角透明()
    {
        using var scene = new WhiteJadeSpiderScene();
        using var normal = Render(scene, Snapshot(PetMode.Normal));
        using var berserk = Render(scene, Snapshot(PetMode.Berserk));

        Assert.InRange(CountOpaque(normal), 500, 256 * 256 / 2);
        Assert.InRange(CountOpaque(berserk), 500, 256 * 256 / 2);
        Assert.Equal((byte)0, normal.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)0, berserk.GetPixel(255, 255).Alpha);
        Assert.True(CountDifferentPixels(normal, berserk) > 2_000);
        SavePreview(normal, "normal-preview.png");
        SavePreview(berserk, "berserk-preview.png");
    }

    [Theory]
    [InlineData(60, 300, 5_000)]
    [InlineData(140, 1_500, 20_000)]
    [InlineData(240, 4_000, 65_000)]
    public void Draw_三档尺寸保持透明边角且完整绘制可见外足(
        int petPixels,
        int minimumOpaquePixels,
        int maximumOpaquePixels)
    {
        var bodyColor = new SKColor(18, 236, 72, 255);
        var snapshot = Snapshot(PetMode.Normal) with
        {
            Scale = petPixels / SpiderDetailLevelSelector.ReferencePetPixels
        };
        var viewport = SpiderViewportMetrics.ForScale(snapshot.Scale);
        var viewportWidth = (int)MathF.Ceiling(viewport.Width);
        var viewportHeight = (int)MathF.Ceiling(viewport.Height);
        var pose = SpiderGeometry.CreatePose(
            viewportWidth,
            viewportHeight,
            snapshot);
        using var atlas = new ContrastingBodyAtlas(bodyColor);
        using var scene = new WhiteJadeSpiderScene(atlas, ownsAtlas: false);
        using var bitmap = Render(
            scene,
            snapshot,
            viewportWidth,
            viewportHeight);

        Assert.Equal((byte)0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(viewportWidth - 1, 0).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(0, viewportHeight - 1).Alpha);
        Assert.Equal(
            (byte)0,
            bitmap.GetPixel(viewportWidth - 1, viewportHeight - 1).Alpha);
        Assert.InRange(
            CountOpaque(bitmap),
            minimumOpaquePixels,
            maximumOpaquePixels);

        Assert.True(
            pose.ContentBounds.Left >= 0
                && pose.ContentBounds.Top >= 0
                && pose.ContentBounds.Right <= viewportWidth
                && pose.ContentBounds.Bottom <= viewportHeight,
            $"{petPixels}px 的完整姿态超出 {viewportWidth}×{viewportHeight} 视口：{pose.ContentBounds}。");

        var verifiedSegments = 0;
        for (var legIndex = 0; legIndex < pose.Legs.Count; legIndex++)
        {
            var leg = pose.Legs[legIndex];
            AssertOuterLegSegment(
                bitmap,
                leg.Hip,
                leg.Knee,
                leg.Width * 0.76f,
                leg.Width * 0.52f,
                bodyColor,
                $"{petPixels}px 第{legIndex + 1}条腿的髋膝段");
            verifiedSegments++;
            AssertOuterLegSegment(
                bitmap,
                leg.Knee,
                leg.Tip,
                leg.Width * 0.52f,
                leg.Width * 0.20f,
                bodyColor,
                $"{petPixels}px 第{legIndex + 1}条腿的膝足段");
            verifiedSegments++;
        }

        Assert.Equal(16, verifiedSegments);
    }

    [Fact]
    public void Draw_三档尺寸在十六量化方向均保留完整透明边带()
    {
        var bodyColor = new SKColor(18, 236, 72, 255);
        using var atlas = new ContrastingBodyAtlas(bodyColor);
        using var scene = new WhiteJadeSpiderScene(atlas, ownsAtlas: false);

        foreach (var petPixels in new[] { 60, 140, 240 })
        {
            var scale =
                petPixels / SpiderDetailLevelSelector.ReferencePetPixels;
            var viewport = SpiderViewportMetrics.ForScale(scale);
            var viewportWidth = (int)MathF.Ceiling(viewport.Width);
            var viewportHeight = (int)MathF.Ceiling(viewport.Height);
            var directionSelector = new SpiderDirectionSelector(
                hysteresisDegrees: 0);
            for (var directionIndex = 0;
                directionIndex < SpiderDirection.Count;
                directionIndex++)
            {
                var angle =
                    directionIndex * MathF.Tau / SpiderDirection.Count;
                var snapshot = Snapshot(PetMode.Normal) with
                {
                    Scale = scale,
                    FacingX = MathF.Sin(angle),
                    FacingY = -MathF.Cos(angle)
                };
                Assert.Equal(
                    directionIndex,
                    directionSelector.Select(
                        snapshot.FacingX,
                        snapshot.FacingY).Index);
                var pose = SpiderGeometry.CreatePose(
                    viewportWidth,
                    viewportHeight,
                    snapshot);
                using var bitmap = Render(
                    scene,
                    snapshot,
                    viewportWidth,
                    viewportHeight);
                var context =
                    $"{petPixels}px 量化方向{directionIndex}";

                AssertTransparentBorder(bitmap, 2, context);
                AssertPoseInsideSafeViewport(
                    pose,
                    viewportWidth,
                    viewportHeight,
                    2,
                    context);
                Assert.Equal(
                    16,
                    CountOuterSegmentsInsideViewport(
                        pose,
                        viewportWidth,
                        viewportHeight,
                        2));
            }
        }
    }

    [Fact]
    public void Draw_相同快照产生确定输出而时间变化会改变步态()
    {
        using var scene = new WhiteJadeSpiderScene();
        using var first = Render(scene, Snapshot(PetMode.Normal, 1.25));
        using var second = Render(scene, Snapshot(PetMode.Normal, 1.25));
        using var later = Render(scene, Snapshot(PetMode.Normal, 1.75));

        Assert.Equal(0, CountDifferentPixels(first, second));
        Assert.True(CountDifferentPixels(first, later) > 100);
    }

    [Fact]
    public void Draw_非法快照数值不会抛出()
    {
        using var scene = new WhiteJadeSpiderScene();
        var snapshot = new RenderSnapshot(
            PetMode.Normal,
            PetMood.Curious,
            float.NaN,
            float.PositiveInfinity,
            double.NaN,
            float.NegativeInfinity,
            "observe");

        var exception = Record.Exception(() => Render(scene, snapshot));

        Assert.Null(exception);
    }

    [Fact]
    public void Normalize_将非有限值与越界视线归一化()
    {
        var normalized = new RenderSnapshot(
            PetMode.Normal,
            PetMood.Curious,
            3,
            float.NaN,
            double.PositiveInfinity,
            -2,
            null!).Normalize();

        Assert.Equal(1, normalized.LookX);
        Assert.Equal(0, normalized.LookY);
        Assert.Equal(0, normalized.AnimationTime);
        Assert.Equal(1, normalized.Scale);
        Assert.Equal(string.Empty, normalized.Behavior);
    }

    [Fact]
    public void Normalize_归一化运动方向速度步相与转弯倾斜()
    {
        var normalized = Snapshot(PetMode.Normal) with
        {
            FacingX = 4,
            FacingY = 3,
            NormalizedSpeed = 2,
            StridePhase = -1,
            TurnLean = float.NegativeInfinity
        };

        normalized = normalized.Normalize();

        Assert.Equal(0.8f, normalized.FacingX, 4);
        Assert.Equal(0.6f, normalized.FacingY, 4);
        Assert.Equal(1, normalized.NormalizedSpeed);
        Assert.Equal(0, normalized.StridePhase);
        Assert.Equal(0, normalized.TurnLean);
    }

    [Fact]
    public void CreatePose_移动步相驱动交替八足且朝向会旋转头部()
    {
        var baseSnapshot = Snapshot(PetMode.Normal, 1.25);
        var still = SpiderGeometry.CreatePose(256, 256, baseSnapshot);
        var moving = SpiderGeometry.CreatePose(256, 256, baseSnapshot with
        {
            NormalizedSpeed = 1,
            StridePhase = 0.25f
        });
        var facingRight = SpiderGeometry.CreatePose(256, 256, baseSnapshot with
        {
            FacingX = 1,
            FacingY = 0
        });

        Assert.NotEqual(still.Legs[0].Tip, moving.Legs[0].Tip);
        Assert.NotEqual(moving.Legs[0].Tip.Y, moving.Legs[1].Tip.Y);
        Assert.True(facingRight.Head.MidX > facingRight.Abdomen.MidX);
    }

    [Fact]
    public void CreatePose_步态固定腿根并逐级放大外部骨点位移()
    {
        var still = SpiderGeometry.CreatePose(256, 256, Snapshot(PetMode.Normal, 0) with
        {
            NormalizedSpeed = 0
        });
        var moving = SpiderGeometry.CreatePose(256, 256, Snapshot(PetMode.Normal, 0) with
        {
            NormalizedSpeed = 1,
            StridePhase = 0.25f
        });

        var stillLeg = still.Legs[0];
        var movingLeg = moving.Legs[0];
        Assert.Equal(stillLeg.Root, movingLeg.Root);
        var hipDisplacement = Distance(stillLeg.Hip, movingLeg.Hip);
        var kneeDisplacement = Distance(stillLeg.Knee, movingLeg.Knee);
        var tipDisplacement = Distance(stillLeg.Tip, movingLeg.Tip);
        Assert.True(0 < hipDisplacement);
        Assert.True(hipDisplacement < kneeDisplacement);
        Assert.True(kneeDisplacement < tipDisplacement);
    }

    [Fact]
    public void CreatePose_移动步态让左右同编号与同侧相邻腿反相()
    {
        var still = SpiderGeometry.CreatePose(256, 256, Snapshot(PetMode.Normal, 0) with
        {
            NormalizedSpeed = 0
        });
        var moving = SpiderGeometry.CreatePose(256, 256, Snapshot(PetMode.Normal, 0) with
        {
            NormalizedSpeed = 1,
            StridePhase = 0.25f
        });

        var firstTipDisplacement = moving.Legs[0].Tip.Y - still.Legs[0].Tip.Y;
        var oppositeSideTipDisplacement = moving.Legs[4].Tip.Y - still.Legs[4].Tip.Y;
        var adjacentTipDisplacement = moving.Legs[1].Tip.Y - still.Legs[1].Tip.Y;
        Assert.True(firstTipDisplacement * oppositeSideTipDisplacement < 0);
        Assert.True(firstTipDisplacement * adjacentTipDisplacement < 0);
    }

    [Fact]
    public void CreatePose_方向旋转保持三段长度与解剖层级()
    {
        var up = SpiderGeometry.CreatePose(256, 256, Snapshot(PetMode.Normal) with
        {
            FacingX = 0,
            FacingY = -1
        });
        var right = SpiderGeometry.CreatePose(256, 256, Snapshot(PetMode.Normal) with
        {
            FacingX = 1,
            FacingY = 0
        });

        for (var index = 0; index < up.Legs.Count; index++)
        {
            var upLeg = up.Legs[index];
            var rightLeg = right.Legs[index];
            Assert.Equal(Distance(upLeg.Root, upLeg.Hip), Distance(rightLeg.Root, rightLeg.Hip), 3);
            Assert.Equal(Distance(upLeg.Hip, upLeg.Knee), Distance(rightLeg.Hip, rightLeg.Knee), 3);
            Assert.Equal(Distance(upLeg.Knee, upLeg.Tip), Distance(rightLeg.Knee, rightLeg.Tip), 3);
            Assert.Equal(upLeg.Layer, rightLeg.Layer);
        }
    }

    [Fact]
    public void Create_动漫桌宠轮廓采用宽伏腹部紧凑头胸与厚实节肢()
    {
        var layout = SpiderLayout.Create(256, 256, 1);

        Assert.True(layout.Abdomen.Width > layout.Abdomen.Height * 1.25f);
        Assert.True(layout.Head.Width < layout.Abdomen.Width * 0.72f);
        Assert.True(layout.Head.MidY < layout.Abdomen.MidY);
        Assert.All(layout.Legs, leg =>
            Assert.True(leg.Width >= layout.Abdomen.Height * 0.16f));
    }

    [Fact]
    public void Create_八足从头胸部两侧形成镜像有序扇形()
    {
        var layout = SpiderLayout.Create(320, 320, 1);

        Assert.Equal(8, layout.Legs.Count);
        Assert.All(layout.Legs, leg =>
        {
            Assert.True(float.IsFinite(leg.Root.X) && float.IsFinite(leg.Root.Y));
            Assert.True(float.IsFinite(leg.Hip.X) && float.IsFinite(leg.Hip.Y));
            Assert.True(float.IsFinite(leg.Knee.X) && float.IsFinite(leg.Knee.Y));
            Assert.True(float.IsFinite(leg.Tip.X) && float.IsFinite(leg.Tip.Y));
            Assert.InRange(leg.Root.Y, layout.Head.Top, layout.Head.Bottom);
            Assert.True(Distance(leg.Root, leg.Hip) > 0);
            Assert.True(Distance(leg.Hip, leg.Knee) > 0);
            Assert.True(Distance(leg.Knee, leg.Tip) > 0);
            Assert.True(Math.Abs(leg.Root.X - layout.Center.X) < Math.Abs(leg.Hip.X - layout.Center.X));
            Assert.True(Math.Abs(leg.Hip.X - layout.Center.X) < Math.Abs(leg.Knee.X - layout.Center.X));
            Assert.True(Math.Abs(leg.Knee.X - layout.Center.X) < Math.Abs(leg.Tip.X - layout.Center.X));
        });

        for (var side = 0; side < 2; side++)
        {
            var legs = layout.Legs.Skip(side * 4).Take(4).ToArray();
            for (var index = 1; index < legs.Length; index++)
            {
                Assert.True(legs[index - 1].Root.Y < legs[index].Root.Y);
                Assert.True(legs[index - 1].Hip.Y < legs[index].Hip.Y);
                Assert.True(legs[index - 1].Knee.Y < legs[index].Knee.Y);
                Assert.True(legs[index - 1].Tip.Y < legs[index].Tip.Y);
            }
        }

        for (var index = 0; index < 4; index++)
        {
            var left = layout.Legs[index];
            var right = layout.Legs[index + 4];
            AssertMirror(left.Root, right.Root, layout.Center.X);
            AssertMirror(left.Hip, right.Hip, layout.Center.X);
            AssertMirror(left.Knee, right.Knee, layout.Center.X);
            AssertMirror(left.Tip, right.Tip, layout.Center.X);
        }
    }

    [Fact]
    public void Create_前两对腿位于身体前景而后两对位于后景()
    {
        var layout = SpiderLayout.Create(320, 320, 1);

        for (var side = 0; side < 2; side++)
        {
            Assert.Equal(SpiderLegLayer.AboveBody, layout.Legs[side * 4].Layer);
            Assert.Equal(SpiderLegLayer.AboveBody, layout.Legs[side * 4 + 1].Layer);
            Assert.Equal(SpiderLegLayer.BehindBody, layout.Legs[side * 4 + 2].Layer);
            Assert.Equal(SpiderLegLayer.BehindBody, layout.Legs[side * 4 + 3].Layer);
        }
    }

    [Fact]
    public void For_常态为冷白灰玉而狂暴态呈血玉内光()
    {
        var normal = SpiderMaterialPalette.For(PetMode.Normal);
        var berserk = SpiderMaterialPalette.For(PetMode.Berserk);

        Assert.True(normal.BodyMiddle.Red >= 210);
        Assert.True(normal.BodyMiddle.Green >= 215);
        Assert.True(normal.BodyMiddle.Blue >= 220);
        Assert.InRange(Math.Abs(normal.BodyMiddle.Green - normal.BodyMiddle.Blue), 0, 18);
        Assert.True(berserk.BodyMiddle.Red > berserk.BodyMiddle.Green * 2);
        Assert.True(berserk.InternalGlow.Red >= 235);
        Assert.True(berserk.InternalGlow.Alpha > normal.InternalGlow.Alpha);
    }

    [Fact]
    public void Draw_默认路径与显式共享姿态产生相同像素()
    {
        using var scene = new WhiteJadeSpiderScene();
        var snapshot = Snapshot(PetMode.Normal, 1.375) with { Mood = PetMood.Alert };
        var pose = SpiderGeometry.CreatePose(256, 256, snapshot);
        using var automatic = Render(scene, snapshot);
        using var explicitPose = new SKBitmap(256, 256, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(explicitPose))
        {
            canvas.Clear(SKColors.Transparent);
            scene.Draw(canvas, snapshot, pose);
        }

        Assert.Equal(0, CountDifferentPixels(automatic, explicitPose));
        var hitMap = SpiderHitMap.Create(pose);
        Assert.All(pose.Legs, leg => Assert.True(hitMap.Contains(leg.Knee.X, leg.Knee.Y)));
    }

    [Fact]
    public void Draw_觅食玩耍与扑跃具有可辨识像素演出()
    {
        using var scene = new WhiteJadeSpiderScene();
        using var forage = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "forage", Phase = BuiltInPhaseKeys.ForageDiscover, PhaseProgress = 0.5
        });
        using var play = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "play", Phase = BuiltInPhaseKeys.PlayBounce, PhaseProgress = 0.5
        });
        using var pounce = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "pounce", Phase = BuiltInPhaseKeys.PounceLeap, PhaseProgress = 0.5
        });
        using var web = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "web", Phase = BuiltInPhaseKeys.WebWeave, PhaseProgress = 0.65, SkillProgress = 0.55
        });
        using var sleep = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "sleep", Phase = BuiltInPhaseKeys.SleepBreathe, PhaseProgress = 0.65
        });

        Assert.True(CountDifferentPixels(forage, play) > 50);
        Assert.True(CountDifferentPixels(play, pounce) > 50);
        SavePreview(forage, "forage-preview.png");
        SavePreview(web, "web-preview.png");
        SavePreview(play, "play-preview.png");
        SavePreview(pounce, "pounce-preview.png");
        SavePreview(sleep, "sleep-preview.png");
    }

    [Fact]
    public void Draw_织网技能随总进度逐步增加丝线()
    {
        using var scene = new WhiteJadeSpiderScene();
        var baseSnapshot = Snapshot(PetMode.Normal) with
        {
            Behavior = "web", Phase = BuiltInPhaseKeys.WebWeave
        };
        using var early = Render(scene, baseSnapshot with { PhaseProgress = 0.15 });
        using var late = Render(scene, baseSnapshot with { PhaseProgress = 0.9 });

        Assert.True(CountOpaque(late) > CountOpaque(early) + 40);
    }

    [Theory]
    [InlineData("forage", BuiltInPhaseKeys.ForageDiscover, BuiltInPhaseKeys.ForageEat)]
    [InlineData("web", BuiltInPhaseKeys.WebAnchor, BuiltInPhaseKeys.WebRest)]
    [InlineData("play", BuiltInPhaseKeys.PlayBounce, BuiltInPhaseKeys.PlayChase)]
    [InlineData("observe", BuiltInPhaseKeys.ObserveTurn, BuiltInPhaseKeys.ObserveTrack)]
    [InlineData("pounce", BuiltInPhaseKeys.PounceCharge, BuiltInPhaseKeys.PounceRetreat)]
    [InlineData("groom", BuiltInPhaseKeys.GroomStart, BuiltInPhaseKeys.GroomFinish)]
    [InlineData("sleep", BuiltInPhaseKeys.SleepCurl, BuiltInPhaseKeys.SleepBreathe)]
    public void Draw_七技能不同阶段产生明确像素差异(
        string behavior,
        string firstPhase,
        string secondPhase)
    {
        using var scene = new WhiteJadeSpiderScene();
        var snapshot = Snapshot(PetMode.Normal, 2.25) with
        {
            Behavior = behavior,
            PhaseProgress = 0.55,
            SkillProgress = 0.55
        };
        using var first = Render(scene, snapshot with { Phase = firstPhase });
        using var second = Render(scene, snapshot with { Phase = secondPhase });

        Assert.True(
            CountDifferentPixels(first, second) > 40,
            $"{behavior} 的 {firstPhase} 与 {secondPhase} 演出像素差异不足。");
        SavePreview(first, $"{behavior}-{firstPhase}-preview.png");
        SavePreview(second, $"{behavior}-{secondPhase}-preview.png");
    }

    private static RenderSnapshot Snapshot(PetMode mode, double time = 1.25) =>
        new(mode, PetMood.Curious, 0.35f, -0.2f, time, 1, "observe");

    private static SKBitmap Render(WhiteJadeSpiderScene scene, RenderSnapshot snapshot)
        => Render(scene, snapshot, 256, 256);

    private static SKBitmap Render(
        WhiteJadeSpiderScene scene,
        RenderSnapshot snapshot,
        int width,
        int height)
    {
        var bitmap = new SKBitmap(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        scene.Draw(canvas, snapshot, width, height);
        return bitmap;
    }

    private static int CountOpaque(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 8)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static float Distance(SKPoint from, SKPoint to)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static void AssertMirror(SKPoint left, SKPoint right, float axisX)
    {
        Assert.Equal(left.Y, right.Y, 3);
        Assert.Equal(axisX - left.X, right.X - axisX, 3);
    }

    private static int CountDifferentPixels(SKBitmap left, SKBitmap right)
    {
        var count = 0;
        for (var y = 0; y < left.Height; y++)
        {
            for (var x = 0; x < left.Width; x++)
            {
                if (left.GetPixel(x, y) != right.GetPixel(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static SKColor PixelAtMidpoint(SKBitmap bitmap, SKPoint start, SKPoint end) =>
        bitmap.GetPixel(
            (int)MathF.Round((start.X + end.X) / 2),
            (int)MathF.Round((start.Y + end.Y) / 2));

    private static void AssertOuterLegSegment(
        SKBitmap bitmap,
        SKPoint start,
        SKPoint end,
        float startWidth,
        float endWidth,
        SKColor bodyColor,
        string description)
    {
        var segment = SpiderLimbGeometry.Create(
            start,
            end,
            startWidth,
            endWidth);
        Assert.True(segment.IsValid, $"{description} 的几何无效。");
        AssertPointInside(bitmap, segment.StartSideA, description);
        AssertPointInside(bitmap, segment.StartSideB, description);
        AssertPointInside(bitmap, segment.EndSideA, description);
        AssertPointInside(bitmap, segment.EndSideB, description);

        // 避开关节端帽与身体遮挡，在目标段内部三个位置独立确认中性玉材像素。
        foreach (var amount in new[] { 0.45f, 0.65f, 0.82f })
        {
            var center = Lerp(start, end, amount);
            var width = startWidth + (endWidth - startWidth) * amount;
            var searchRadius = Math.Max(1, (int)MathF.Ceiling(width * 0.18f));
            Assert.True(
                HasJadeLegPixelNear(
                    bitmap,
                    center,
                    searchRadius,
                    bodyColor),
                $"{description} 在中心线 {amount:P0} 附近没有检测到异于身体图集的中性玉足像素。");
        }

        // 从段中心沿两侧法线越过最大半宽；至少一侧回到透明背景，排除“大块身体碰巧不透明”。
        var directionX = end.X - start.X;
        var directionY = end.Y - start.Y;
        var length = MathF.Sqrt(directionX * directionX + directionY * directionY);
        Assert.True(length > 0, $"{description} 长度必须大于零。");
        var normal = new SKPoint(directionY / length, -directionX / length);
        const float outsideAmount = 0.72f;
        var outsideCenter = Lerp(start, end, outsideAmount);
        var outsideWidth =
            startWidth + (endWidth - startWidth) * outsideAmount;
        var outsideDistance = Math.Max(5, outsideWidth * 1.4f);
        var sideA = new SKPoint(
            outsideCenter.X + normal.X * outsideDistance,
            outsideCenter.Y + normal.Y * outsideDistance);
        var sideB = new SKPoint(
            outsideCenter.X - normal.X * outsideDistance,
            outsideCenter.Y - normal.Y * outsideDistance);
        Assert.True(
            HasOnlyLowAlphaNear(bitmap, sideA, 1)
                || HasOnlyLowAlphaNear(bitmap, sideB, 1),
            $"{description} 两侧法线外采样均被大块不透明区域占据，无法证明目标是细长外足。");
    }

    private static void AssertPointInside(
        SKBitmap bitmap,
        SKPoint point,
        string description) =>
        Assert.True(
            point.X >= 0
                && point.Y >= 0
                && point.X < bitmap.Width
                && point.Y < bitmap.Height,
            $"{description} 的边界点 ({point.X:F1}, {point.Y:F1}) 超出视口。");

    private static bool HasJadeLegPixelNear(
        SKBitmap bitmap,
        SKPoint center,
        int radius,
        SKColor bodyColor)
    {
        var centerX = (int)MathF.Round(center.X);
        var centerY = (int)MathF.Round(center.Y);
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
                {
                    continue;
                }

                var pixel = bitmap.GetPixel(x, y);
                var maximumChannel = Math.Max(
                    pixel.Red,
                    Math.Max(pixel.Green, pixel.Blue));
                var minimumChannel = Math.Min(
                    pixel.Red,
                    Math.Min(pixel.Green, pixel.Blue));
                var bodyDistance =
                    Math.Abs(pixel.Red - bodyColor.Red)
                    + Math.Abs(pixel.Green - bodyColor.Green)
                    + Math.Abs(pixel.Blue - bodyColor.Blue);
                if (pixel.Alpha >= 24
                    && maximumChannel - minimumChannel <= 80
                    && bodyDistance >= 120)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasOnlyLowAlphaNear(
        SKBitmap bitmap,
        SKPoint center,
        int radius)
    {
        var centerX = (int)MathF.Round(center.X);
        var centerY = (int)MathF.Round(center.Y);
        if (centerX - radius < 0
            || centerY - radius < 0
            || centerX + radius >= bitmap.Width
            || centerY + radius >= bitmap.Height)
        {
            return false;
        }

        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 16)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AssertTransparentBorder(
        SKBitmap bitmap,
        int thickness,
        string context)
    {
        Assert.InRange(thickness, 1, Math.Min(bitmap.Width, bitmap.Height) / 2);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var offset = 0; offset < thickness; offset++)
            {
                AssertBorderPixel(bitmap, offset, y, context);
                AssertBorderPixel(
                    bitmap,
                    bitmap.Width - 1 - offset,
                    y,
                    context);
            }
        }

        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var offset = 0; offset < thickness; offset++)
            {
                AssertBorderPixel(bitmap, x, offset, context);
                AssertBorderPixel(
                    bitmap,
                    x,
                    bitmap.Height - 1 - offset,
                    context);
            }
        }
    }

    private static void AssertBorderPixel(
        SKBitmap bitmap,
        int x,
        int y,
        string context) =>
        Assert.True(
            bitmap.GetPixel(x, y).Alpha <= 8,
            $"{context} 在边带 ({x},{y}) 出现不透明像素。");

    private static void AssertPoseInsideSafeViewport(
        SpiderPose pose,
        int viewportWidth,
        int viewportHeight,
        float safePadding,
        string context) =>
        Assert.True(
            pose.ContentBounds.Left >= safePadding
                && pose.ContentBounds.Top >= safePadding
                && pose.ContentBounds.Right <= viewportWidth - safePadding
                && pose.ContentBounds.Bottom <= viewportHeight - safePadding,
            $"{context} 的姿态范围 {pose.ContentBounds} 未留出 {safePadding}px 安全边带。");

    private static int CountOuterSegmentsInsideViewport(
        SpiderPose pose,
        int viewportWidth,
        int viewportHeight,
        float safePadding)
    {
        var count = 0;
        foreach (var leg in pose.Legs)
        {
            count += IsSegmentInsideViewport(
                SpiderLimbGeometry.Create(
                    leg.Hip,
                    leg.Knee,
                    leg.Width * 0.76f,
                    leg.Width * 0.52f),
                viewportWidth,
                viewportHeight,
                safePadding) ? 1 : 0;
            count += IsSegmentInsideViewport(
                SpiderLimbGeometry.Create(
                    leg.Knee,
                    leg.Tip,
                    leg.Width * 0.52f,
                    leg.Width * 0.20f),
                viewportWidth,
                viewportHeight,
                safePadding) ? 1 : 0;
        }

        return count;
    }

    private static bool IsSegmentInsideViewport(
        SpiderLimbSegment segment,
        int viewportWidth,
        int viewportHeight,
        float safePadding) =>
        segment.IsValid
        && IsPointInsideViewport(
            segment.StartSideA,
            viewportWidth,
            viewportHeight,
            safePadding)
        && IsPointInsideViewport(
            segment.StartSideB,
            viewportWidth,
            viewportHeight,
            safePadding)
        && IsPointInsideViewport(
            segment.EndSideA,
            viewportWidth,
            viewportHeight,
            safePadding)
        && IsPointInsideViewport(
            segment.EndSideB,
            viewportWidth,
            viewportHeight,
            safePadding);

    private static bool IsPointInsideViewport(
        SKPoint point,
        int viewportWidth,
        int viewportHeight,
        float safePadding) =>
        point.X >= safePadding
        && point.Y >= safePadding
        && point.X <= viewportWidth - safePadding
        && point.Y <= viewportHeight - safePadding;

    private static SKPoint Lerp(SKPoint start, SKPoint end, float amount) =>
        new(
            start.X + (end.X - start.X) * amount,
            start.Y + (end.Y - start.Y) * amount);

    private static string ReadSceneSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "Honey.Rendering",
                "Spider",
                "WhiteJadeSpiderScene.cs");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WhiteJadeSpiderScene.cs。");
    }

    private static void SavePreview(SKBitmap bitmap, string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "honey-task7-stage-preview");
        Directory.CreateDirectory(directory);
        var finalPath = Path.Combine(directory, fileName);
        var temporaryPath = Path.Combine(
            directory,
            $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
            }

            File.Move(temporaryPath, finalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed class ContrastingBodyAtlas : ISpiderBodyAtlas
    {
        private readonly SKBitmap _bitmap;

        public ContrastingBodyAtlas(SKColor color)
        {
            _bitmap = new SKBitmap(
                128,
                128,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using var canvas = new SKCanvas(_bitmap);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = color,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawOval(new SKRect(18, 38, 110, 112), paint);
            canvas.DrawOval(new SKRect(39, 8, 89, 58), paint);
        }

        public bool TryGetFrame(
            PetMode mode,
            SpiderDirection direction,
            out SpiderAtlasFrame frame)
        {
            frame = new SpiderAtlasFrame(
                _bitmap,
                new SKRectI(0, 0, _bitmap.Width, _bitmap.Height),
                new SKPoint(0.5f, 0.54f));
            return true;
        }

        public void Dispose() => _bitmap.Dispose();
    }

    private sealed class SolidBodyAtlas(SKColor color) : ISpiderBodyAtlas
    {
        private readonly SKBitmap _bitmap = CreateBitmap(color);

        public bool TryGetFrame(
            PetMode mode,
            SpiderDirection direction,
            out SpiderAtlasFrame frame)
        {
            frame = new SpiderAtlasFrame(
                _bitmap,
                new SKRectI(0, 0, _bitmap.Width, _bitmap.Height),
                new SKPoint(0.5f, 0.5f));
            return true;
        }

        public void Dispose() => _bitmap.Dispose();

        private static SKBitmap CreateBitmap(SKColor color)
        {
            var bitmap = new SKBitmap(
                8,
                8,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            bitmap.Erase(color);
            return bitmap;
        }
    }
}
