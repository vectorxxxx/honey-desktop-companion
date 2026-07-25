using Honey.Domain.Model;
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
            Behavior = "forage", Phase = "发现灵蝶", PhaseProgress = 0.5
        });
        using var play = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "play", Phase = "丝球弹跳", PhaseProgress = 0.5
        });
        using var pounce = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "pounce", Phase = "短跳", PhaseProgress = 0.5
        });
        using var web = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "web", Phase = "往返织网", PhaseProgress = 0.65, SkillProgress = 0.55
        });
        using var sleep = Render(scene, Snapshot(PetMode.Normal) with
        {
            Behavior = "sleep", Phase = "呼吸光", PhaseProgress = 0.65
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
            Behavior = "web", Phase = "往返织网", PhaseProgress = 0.5
        };
        using var early = Render(scene, baseSnapshot with { SkillProgress = 0.15 });
        using var late = Render(scene, baseSnapshot with { SkillProgress = 0.9 });

        Assert.True(CountOpaque(late) > CountOpaque(early) + 40);
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
        var directory = Path.Combine(Path.GetTempPath(), "honey-task6-preview");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, fileName));
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
    }
}
