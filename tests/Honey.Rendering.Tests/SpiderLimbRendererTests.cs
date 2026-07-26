using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderLimbRendererTests
{
    [Fact]
    public void DrawSegment_根部像素宽于末端且四角透明()
    {
        using var renderer = new SpiderLimbRenderer();
        using var bitmap = RenderSegment(renderer, PetMode.Normal);

        Assert.True(VerticalOpaqueSpan(bitmap, 45) > VerticalOpaqueSpan(bitmap, 165));
        Assert.Equal((byte)0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(219, 0).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(0, 119).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(219, 119).Alpha);
    }

    [Fact]
    public void DrawSegment_普通白玉与狂暴血玉明显不同()
    {
        using var renderer = new SpiderLimbRenderer();
        using var normal = RenderSegment(renderer, PetMode.Normal);
        using var berserk = RenderSegment(renderer, PetMode.Berserk);

        Assert.True(CountDifferentPixels(normal, berserk) > 800);
    }

    [Fact]
    public void DrawSegment_左右反向绘制时高光都位于屏幕上缘()
    {
        using var renderer = new SpiderLimbRenderer();
        using var leftToRight = RenderUniformSegment(
            renderer,
            new SKPoint(25, 60),
            new SKPoint(195, 60));
        using var rightToLeft = RenderUniformSegment(
            renderer,
            new SKPoint(195, 60),
            new SKPoint(25, 60));

        Assert.True(
            AverageBrightness(leftToRight, 50) > AverageBrightness(leftToRight, 70),
            "从左向右的肢段高光应偏向屏幕上缘。");
        Assert.True(
            AverageBrightness(rightToLeft, 50) > AverageBrightness(rightToLeft, 70),
            "从右向左的肢段高光不应随段方向翻转到下缘。");
    }

    [Fact]
    public void Dispose_重复调用不会抛出()
    {
        var renderer = new SpiderLimbRenderer();

        renderer.Dispose();
        var exception = Record.Exception(renderer.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void DrawSegment_释放后抛出对象已释放异常()
    {
        var renderer = new SpiderLimbRenderer();
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        var segment = SpiderLimbGeometry.Create(
            new SKPoint(4, 16),
            new SKPoint(28, 16),
            8,
            2);
        renderer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => renderer.DrawSegment(
            canvas,
            segment,
            SpiderMaterialPalette.For(PetMode.Normal),
            SpiderDetailLevel.Standard));
    }

    private static SKBitmap RenderSegment(
        SpiderLimbRenderer renderer,
        PetMode mode)
    {
        var bitmap = new SKBitmap(220, 120, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        renderer.DrawSegment(
            canvas,
            SpiderLimbGeometry.Create(
                new SKPoint(25, 60),
                new SKPoint(195, 60),
                30,
                6),
            SpiderMaterialPalette.For(mode),
            SpiderDetailLevel.Showcase);
        return bitmap;
    }

    private static SKBitmap RenderUniformSegment(
        SpiderLimbRenderer renderer,
        SKPoint start,
        SKPoint end)
    {
        var bitmap = new SKBitmap(220, 120, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        renderer.DrawSegment(
            canvas,
            SpiderLimbGeometry.Create(start, end, 24, 24),
            SpiderMaterialPalette.For(PetMode.Normal),
            SpiderDetailLevel.Standard);
        return bitmap;
    }

    private static int VerticalOpaqueSpan(SKBitmap bitmap, int x)
    {
        var rows = Enumerable.Range(0, bitmap.Height)
            .Where(y => bitmap.GetPixel(x, y).Alpha > 8)
            .ToArray();
        return rows.Length == 0 ? 0 : rows[^1] - rows[0] + 1;
    }

    private static double AverageBrightness(SKBitmap bitmap, int y)
    {
        var sum = 0d;
        var count = 0;
        for (var x = 50; x <= 170; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Alpha <= 8)
            {
                continue;
            }

            sum += pixel.Red * 0.2126 + pixel.Green * 0.7152 + pixel.Blue * 0.0722;
            count++;
        }

        return count == 0 ? 0 : sum / count;
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
}
