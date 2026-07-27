using Honey.Domain.Model;
using Honey.Rendering;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class WhiteJadeSpiderDirectionalRegressionTests
{
    [Fact]
    public void 向上斜向与向右会使用不同精绘方向且中心保持稳定()
    {
        using var scene = new WhiteJadeSpiderScene();
        using var up = Render(scene, Snapshot(0, -1));
        using var diagonal = Render(scene, Snapshot(0.707f, -0.707f));
        using var right = Render(scene, Snapshot(1, 0));

        Assert.True(CountDifferentPixels(up, diagonal) > 1_500);
        Assert.True(CountDifferentPixels(diagonal, right) > 1_500);
        var upCenter = CenterOfSolidPixels(up);
        var diagonalCenter = CenterOfSolidPixels(diagonal);
        var rightCenter = CenterOfSolidPixels(right);
        Assert.InRange(upCenter.X - diagonalCenter.X, -10f, 10f);
        Assert.InRange(upCenter.Y - diagonalCenter.Y, -10f, 10f);
        Assert.InRange(rightCenter.X - diagonalCenter.X, -10f, 10f);
        Assert.InRange(rightCenter.Y - diagonalCenter.Y, -10f, 10f);
        SavePreview(up, "normal-up.png");
        SavePreview(diagonal, "normal-diagonal.png");
        SavePreview(right, "normal-right.png");
    }

    [Fact]
    public void 狂暴斜向会绘制足量血玉像素并保存预览()
    {
        using var scene = new WhiteJadeSpiderScene();
        using var berserk = Render(
            scene,
            Snapshot(0.707f, -0.707f) with { Mode = PetMode.Berserk });

        Assert.True(CountOpaque(berserk) > 500);
        SavePreview(berserk, "berserk-diagonal.png");
    }

    [Fact]
    public void 狂暴斜向的十六个外足段均呈红玉材质且区别于普通态()
    {
        var bodyColor = new SKColor(14, 210, 235, 255);
        var berserkSnapshot = Snapshot(0.707f, -0.707f) with
        {
            Mode = PetMode.Berserk,
            AnimationTime = 0,
            Behavior = string.Empty
        };
        var normalSnapshot = berserkSnapshot with { Mode = PetMode.Normal };
        var viewport =
            SpiderViewportMetrics.ForScale(berserkSnapshot.Scale);
        var viewportWidth = (int)MathF.Ceiling(viewport.Width);
        var viewportHeight = (int)MathF.Ceiling(viewport.Height);
        var pose = SpiderGeometry.CreatePose(
            viewportWidth,
            viewportHeight,
            berserkSnapshot);
        using var atlas = new ContrastingBodyAtlas(bodyColor);
        using var scene = new WhiteJadeSpiderScene(atlas, ownsAtlas: false);
        using var berserk = Render(
            scene,
            berserkSnapshot,
            viewportWidth,
            viewportHeight);
        using var normal = Render(
            scene,
            normalSnapshot,
            viewportWidth,
            viewportHeight);

        var verifiedSegments = 0;
        for (var legIndex = 0; legIndex < pose.Legs.Count; legIndex++)
        {
            var leg = pose.Legs[legIndex];
            AssertBerserkSegmentMaterial(
                berserk,
                normal,
                leg.Hip,
                leg.Knee,
                leg.Width * 0.76f,
                bodyColor,
                $"第{legIndex + 1}条腿的髋膝段");
            verifiedSegments++;
            AssertBerserkSegmentMaterial(
                berserk,
                normal,
                leg.Knee,
                leg.Tip,
                leg.Width * 0.52f,
                bodyColor,
                $"第{legIndex + 1}条腿的膝足段");
            verifiedSegments++;
        }

        Assert.Equal(16, verifiedSegments);
    }

    [Fact]
    public void 场景会把量化并校准后的方向交给图集()
    {
        using var atlas = new TrackingAtlas();
        using var scene = new WhiteJadeSpiderScene(atlas, ownsAtlas: false);
        using var up = Render(scene, Snapshot(0, -1));
        using var diagonal = Render(scene, Snapshot(0.707f, -0.707f));
        using var right = Render(scene, Snapshot(1, 0));

        Assert.Equal([12, 9, 6], atlas.RequestedDirections);
    }

    [Fact]
    public void 图集不可用时程序回退仍能绘制()
    {
        using var scene = new WhiteJadeSpiderScene(new MissingAtlas());
        using var bitmap = Render(scene, Snapshot(1, 0));

        Assert.True(CountOpaque(bitmap) > 500);
    }

    private static RenderSnapshot Snapshot(float x, float y) =>
        new(
            PetMode.Normal,
            PetMood.Curious,
            0,
            0,
            1.25,
            1,
            "observe",
            FacingX: x,
            FacingY: y);

    private static SKBitmap Render(
        WhiteJadeSpiderScene scene,
        RenderSnapshot snapshot)
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

    private static void AssertBerserkSegmentMaterial(
        SKBitmap berserk,
        SKBitmap normal,
        SKPoint start,
        SKPoint end,
        float segmentWidth,
        SKColor bodyColor,
        string description)
    {
        var berserkRedSamples = 0;
        var normalNeutralSamples = 0;
        var normalRedSamples = 0;
        // 后景腿贴近头胸部的一端会按设计被遮挡，因此取外端三个内部位置避免身体遮挡。
        foreach (var amount in new[] { 0.85f, 0.90f, 0.95f })
        {
            var sample = new SKPoint(
                start.X + (end.X - start.X) * amount,
                start.Y + (end.Y - start.Y) * amount);
            var radius = Math.Max(
                1,
                (int)MathF.Ceiling(segmentWidth * 0.18f));
            berserkRedSamples += HasPixelNear(
                berserk,
                sample,
                radius,
                pixel => IsRedJadePixel(pixel, bodyColor)) ? 1 : 0;
            normalNeutralSamples += HasPixelNear(
                normal,
                sample,
                radius,
                pixel => IsNeutralJadePixel(pixel, bodyColor)) ? 1 : 0;
            normalRedSamples += HasPixelNear(
                normal,
                sample,
                radius,
                pixel => IsRedJadePixel(pixel, bodyColor)) ? 1 : 0;
        }

        Assert.True(
            berserkRedSamples >= 2,
            $"{description} 只有 {berserkRedSamples}/3 个内部采样点呈红玉材质。");
        Assert.True(
            normalNeutralSamples >= 2,
            $"{description} 的普通态缺少中性白玉负对照。");
        Assert.Equal(0, normalRedSamples);
    }

    private static bool HasPixelNear(
        SKBitmap bitmap,
        SKPoint center,
        int radius,
        Func<SKColor, bool> predicate)
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
                if (predicate(bitmap.GetPixel(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsRedJadePixel(SKColor pixel, SKColor bodyColor) =>
        pixel.Alpha >= 24
        && pixel.Red >= pixel.Green + 24
        && pixel.Red >= pixel.Blue + 18
        && ColorDistance(pixel, bodyColor) >= 120;

    private static bool IsNeutralJadePixel(
        SKColor pixel,
        SKColor bodyColor)
    {
        var maximumChannel = Math.Max(
            pixel.Red,
            Math.Max(pixel.Green, pixel.Blue));
        var minimumChannel = Math.Min(
            pixel.Red,
            Math.Min(pixel.Green, pixel.Blue));
        return pixel.Alpha >= 24
            && maximumChannel - minimumChannel <= 80
            && ColorDistance(pixel, bodyColor) >= 120;
    }

    private static int ColorDistance(SKColor pixel, SKColor reference) =>
        Math.Abs(pixel.Red - reference.Red)
        + Math.Abs(pixel.Green - reference.Green)
        + Math.Abs(pixel.Blue - reference.Blue);

    private static SKPoint CenterOfSolidPixels(SKBitmap bitmap)
    {
        double sumX = 0;
        double sumY = 0;
        var count = 0;
        // 过滤低透明粒子与抗锯齿边缘，避免方向无关特效干扰主体中心。
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha < 128)
                {
                    continue;
                }

                sumX += x;
                sumY += y;
                count++;
            }
        }

        Assert.True(count > 0);
        return new SKPoint(
            (float)(sumX / count),
            (float)(sumY / count));
    }

    private static void SavePreview(SKBitmap bitmap, string fileName)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "honey-pet-codex-preview");
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

    private sealed class MissingAtlas : ISpiderBodyAtlas
    {
        public bool TryGetFrame(
            PetMode mode,
            SpiderDirection direction,
            out SpiderAtlasFrame frame)
        {
            frame = default;
            return false;
        }

        public void Dispose()
        {
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

    private sealed class TrackingAtlas : ISpiderBodyAtlas
    {
        private readonly SKBitmap _bitmap = new(
            64,
            64,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        public List<int> RequestedDirections { get; } = [];

        public bool TryGetFrame(
            PetMode mode,
            SpiderDirection direction,
            out SpiderAtlasFrame frame)
        {
            RequestedDirections.Add(direction.Index);
            _bitmap.Erase(new SKColor(
                (byte)(40 + direction.Index * 10),
                180,
                210,
                255));
            frame = new SpiderAtlasFrame(
                _bitmap,
                SKRectI.Create(0, 0, 64, 64),
                new SKPoint(0.5f, 0.54f));
            return true;
        }

        public void Dispose() => _bitmap.Dispose();
    }
}
