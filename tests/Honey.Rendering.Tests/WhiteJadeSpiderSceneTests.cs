using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class WhiteJadeSpiderSceneTests
{
    [Fact]
    public void Draw_普通与狂暴有明显像素差异且四角透明()
    {
        var scene = new WhiteJadeSpiderScene();
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
        var scene = new WhiteJadeSpiderScene();
        using var first = Render(scene, Snapshot(PetMode.Normal, 1.25));
        using var second = Render(scene, Snapshot(PetMode.Normal, 1.25));
        using var later = Render(scene, Snapshot(PetMode.Normal, 1.75));

        Assert.Equal(0, CountDifferentPixels(first, second));
        Assert.True(CountDifferentPixels(first, later) > 100);
    }

    [Fact]
    public void Draw_非法快照数值不会抛出()
    {
        var scene = new WhiteJadeSpiderScene();
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
