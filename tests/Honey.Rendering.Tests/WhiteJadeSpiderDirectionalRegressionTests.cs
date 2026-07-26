using Honey.Domain.Model;
using Honey.Rendering;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class WhiteJadeSpiderDirectionalRegressionTests
{
    [Fact]
    public void 向上与斜向会使用不同精绘方向且中心保持稳定()
    {
        using var scene = new WhiteJadeSpiderScene();
        using var up = Render(scene, Snapshot(0, -1));
        using var diagonal = Render(scene, Snapshot(0.707f, -0.707f));

        Assert.True(CountDifferentPixels(up, diagonal) > 1_500);
        var upCenter = CenterOfOpaque(up);
        var diagonalCenter = CenterOfOpaque(diagonal);
        Assert.InRange(upCenter.X - diagonalCenter.X, -8, 8);
        Assert.InRange(upCenter.Y - diagonalCenter.Y, -8, 8);
        SavePreview(up, "normal-up.png");
        SavePreview(diagonal, "normal-diagonal.png");
    }

    [Fact]
    public void 场景会把量化并校准后的方向交给图集()
    {
        using var atlas = new TrackingAtlas();
        using var scene = new WhiteJadeSpiderScene(atlas, ownsAtlas: false);
        using var up = Render(scene, Snapshot(0, -1));
        using var diagonal = Render(scene, Snapshot(0.707f, -0.707f));

        Assert.Equal([12, 9], atlas.RequestedDirections);
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
    {
        var bitmap = new SKBitmap(
            256,
            256,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
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

    private static SKPointI CenterOfOpaque(SKBitmap bitmap)
    {
        long sumX = 0;
        long sumY = 0;
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha <= 8)
                {
                    continue;
                }

                sumX += x;
                sumY += y;
                count++;
            }
        }

        return new SKPointI((int)(sumX / count), (int)(sumY / count));
    }

    private static void SavePreview(SKBitmap bitmap, string fileName)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "honey-pet-codex-preview");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, fileName));
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
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
