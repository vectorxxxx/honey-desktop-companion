using Honey.Domain.Model;
using Honey.Rendering;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class WhiteJadeSpiderCompactPreviewTests
{
    private static readonly SKColor Background = new(31, 31, 31, 255);

    [Fact]
    public void Draw_最小尺寸十六方向均有主体并保存总览()
    {
        const int cell = 128;
        using var scene = new WhiteJadeSpiderScene();
        using var sheet = NewBitmap(cell * 4, cell * 4);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(Background);

        for (var index = 0; index < 16; index++)
        {
            var angle = index * MathF.Tau / 16;
            var snapshot = Snapshot(60f / 140f) with
            {
                FacingX = MathF.Sin(angle),
                FacingY = -MathF.Cos(angle)
            };
            using var frame = Render(scene, snapshot, cell, cell);

            Assert.True(
                CountChangedPixels(frame) >= 300,
                $"60px 量化方向 {index} 的主体像素不足。" );
            DrawFrame(canvas, frame, (index % 4) * cell, (index / 4) * cell);
        }

        var path = Save(sheet, "compact-60px-16-directions.png");
        AssertPreviewWritten(path);
    }

    [Fact]
    public void Draw_最小尺寸静止与最大步态均有主体并保存对比()
    {
        const int cell = 160;
        using var scene = new WhiteJadeSpiderScene();
        using var sheet = NewBitmap(cell * 2, cell);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(Background);
        var still = Snapshot(60f / 140f);
        var moving = still with
        {
            Mood = PetMood.Alert,
            NormalizedSpeed = 1,
            StridePhase = 0.25f
        };

        using var stillFrame = Render(scene, still, cell, cell);
        using var movingFrame = Render(scene, moving, cell, cell);
        Assert.True(CountChangedPixels(stillFrame) >= 300, "60px 静止姿态的主体像素不足。");
        Assert.True(CountChangedPixels(movingFrame) >= 300, "60px 最大步态的主体像素不足。");
        DrawFrame(canvas, stillFrame, 0, 0);
        DrawFrame(canvas, movingFrame, cell, 0);

        var path = Save(sheet, "compact-60px-still-moving.png");
        AssertPreviewWritten(path);
    }

    [Fact]
    public void Draw_三种设置尺寸均有主体并保存对比()
    {
        const int cell = 400;
        using var scene = new WhiteJadeSpiderScene();
        using var sheet = NewBitmap(cell * 3, cell);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(Background);
        var scales = new[] { 60f / 140f, 1f, 240f / 140f };

        for (var index = 0; index < scales.Length; index++)
        {
            using var frame = Render(scene, Snapshot(scales[index]), cell, cell);
            Assert.True(
                CountChangedPixels(frame) >= 300,
                $"{new[] { 60, 140, 240 }[index]}px 的主体像素不足。" );
            DrawFrame(canvas, frame, index * cell, 0);
        }

        var path = Save(sheet, "size-60-140-240px.png");
        AssertPreviewWritten(path);
    }

    private static RenderSnapshot Snapshot(float scale) => new(
        PetMode.Normal,
        PetMood.Curious,
        0,
        0,
        1.25,
        scale,
        string.Empty,
        FacingX: 1,
        FacingY: 0);

    private static SKBitmap Render(
        WhiteJadeSpiderScene scene,
        RenderSnapshot snapshot,
        int width,
        int height)
    {
        var bitmap = NewBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background);
        scene.Draw(canvas, snapshot, width, height);
        return bitmap;
    }

    private static SKBitmap NewBitmap(int width, int height) => new(
        width,
        height,
        SKColorType.Bgra8888,
        SKAlphaType.Premul);

    private static void DrawFrame(SKCanvas canvas, SKBitmap frame, int x, int y)
    {
        var source = SKRect.Create(0, 0, frame.Width, frame.Height);
        var destination = SKRect.Create(x, y, frame.Width, frame.Height);
        var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        canvas.DrawBitmap(frame, source, destination, sampling, null);
    }

    private static int CountChangedPixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != Background)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string Save(SKBitmap bitmap, string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "honey-pet-codex-preview");
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
            return finalPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void AssertPreviewWritten(string path)
    {
        var file = new FileInfo(path);
        Assert.True(file.Exists && file.Length > 0, $"预览文件未写入：{path}");
    }
}
