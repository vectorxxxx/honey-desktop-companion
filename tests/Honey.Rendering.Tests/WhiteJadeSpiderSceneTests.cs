using Honey.Domain.Model;
using Honey.Domain.Behavior;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class WhiteJadeSpiderSceneTests
{
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
        Assert.NotEqual(stillLeg.Hip, movingLeg.Hip);
        Assert.NotEqual(stillLeg.Knee, movingLeg.Knee);
        Assert.NotEqual(stillLeg.Tip, movingLeg.Tip);
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
    public void Create_八足采用隐藏根部与三段扇形布局()
    {
        var layout = SpiderLayout.Create(320, 320, 1);

        Assert.Equal(8, layout.Legs.Count);
        Assert.All(layout.Legs, leg =>
        {
            Assert.True(float.IsFinite(leg.Root.X) && float.IsFinite(leg.Root.Y));
            Assert.True(float.IsFinite(leg.Hip.X) && float.IsFinite(leg.Hip.Y));
            Assert.True(float.IsFinite(leg.Knee.X) && float.IsFinite(leg.Knee.Y));
            Assert.True(float.IsFinite(leg.Tip.X) && float.IsFinite(leg.Tip.Y));
            Assert.True(Distance(leg.Root, leg.Hip) > 0);
            Assert.True(Distance(leg.Hip, leg.Knee) > 0);
            Assert.True(Distance(leg.Knee, leg.Tip) > 0);
            Assert.True(Math.Abs(leg.Root.X - layout.Center.X) < Math.Abs(leg.Hip.X - layout.Center.X));
        });

        for (var side = 0; side < 2; side++)
        {
            var legs = layout.Legs.Skip(side * 4).Take(4).ToArray();
            for (var index = 1; index < legs.Length; index++)
            {
                Assert.True(legs[index - 1].Hip.Y < legs[index].Hip.Y);
            }
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
    {
        var bitmap = new SKBitmap(256, 256, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        scene.Draw(canvas, snapshot);
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

    private static void SavePreview(SKBitmap bitmap, string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "honey-task7-stage-preview");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, fileName));
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
    }
}
