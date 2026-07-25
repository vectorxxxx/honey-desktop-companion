using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderDpiScalingTests
{
    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    [InlineData(2.0f)]
    public void CreatePose_归一化后的中心几何与线宽在不同Dpi下一致(float density)
    {
        var viewport = SpiderViewportMetrics.ForScale(1);
        var snapshot = new RenderSnapshot(
            PetMode.Normal,
            PetMood.Alert,
            0,
            0,
            0.75,
            1,
            "observe");
        var reference = SpiderGeometry.CreatePose(viewport.Width, viewport.Height, snapshot, 1);
        var pose = SpiderGeometry.CreatePose(
            viewport.Width * density,
            viewport.Height * density,
            snapshot,
            density);

        Assert.Equal(reference.Center.X, pose.Center.X / density, 3);
        Assert.Equal(reference.Center.Y, pose.Center.Y / density, 3);
        Assert.Equal(reference.ContentBounds.Left, pose.ContentBounds.Left / density, 3);
        Assert.Equal(reference.ContentBounds.Top, pose.ContentBounds.Top / density, 3);
        Assert.Equal(reference.ContentBounds.Right, pose.ContentBounds.Right / density, 3);
        Assert.Equal(reference.ContentBounds.Bottom, pose.ContentBounds.Bottom / density, 3);
        for (var index = 0; index < reference.Legs.Count; index++)
        {
            Assert.Equal(reference.Legs[index].Knee.X, pose.Legs[index].Knee.X / density, 3);
            Assert.Equal(reference.Legs[index].Knee.Y, pose.Legs[index].Knee.Y / density, 3);
            Assert.Equal(reference.Legs[index].Width, pose.Legs[index].Width / density, 3);
        }
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    [InlineData(2.0f)]
    public void CreatePose_二倍宠物在不同Dpi视口中均不裁切(float density)
    {
        var viewport = SpiderViewportMetrics.ForScale(2);
        var snapshot = new RenderSnapshot(
            PetMode.Berserk,
            PetMood.Angry,
            0,
            0,
            1.25,
            2,
            "observe");
        var pose = SpiderGeometry.CreatePose(
            viewport.Width * density,
            viewport.Height * density,
            snapshot,
            density);

        Assert.True(pose.ContentBounds.Left >= 0);
        Assert.True(pose.ContentBounds.Top >= 0);
        Assert.True(pose.ContentBounds.Right <= viewport.Width * density);
        Assert.True(pose.ContentBounds.Bottom <= viewport.Height * density);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void CreatePose_非法设备密度安全回退为一倍(float invalidDensity)
    {
        var snapshot = new RenderSnapshot(
            PetMode.Normal,
            PetMood.Curious,
            0,
            0,
            0,
            1,
            "observe");
        var expected = SpiderGeometry.CreatePose(320, 320, snapshot, 1);
        var actual = SpiderGeometry.CreatePose(320, 320, snapshot, invalidDensity);

        Assert.Equal(expected.ContentBounds, actual.ContentBounds);
        Assert.Equal(1, actual.DeviceScale);
    }

    [Fact]
    public void Draw_高分辨率输出降采样后的可见边界一致并生成预览()
    {
        var viewport = SpiderViewportMetrics.ForScale(1);
        var snapshot = new RenderSnapshot(
            PetMode.Normal,
            PetMood.Alert,
            0.25f,
            -0.25f,
            0.75,
            1,
            "observe");
        var directory = Path.Combine(Path.GetTempPath(), "honey-task6-preview");
        Directory.CreateDirectory(directory);
        NormalizedBounds? expected = null;
        foreach (var density in new[] { 1f, 1.5f, 2f })
        {
            var width = (int)MathF.Round(viewport.Width * density);
            var height = (int)MathF.Round(viewport.Height * density);
            var pose = SpiderGeometry.CreatePose(width, height, snapshot, density);
            using var bitmap = new SKBitmap(
                width,
                height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            using (var scene = new WhiteJadeSpiderScene())
            {
                canvas.Clear(SKColors.Transparent);
                scene.Draw(canvas, snapshot, pose);
            }

            var bounds = VisibleBounds(bitmap, density);
            if (expected is { } reference)
            {
                Assert.InRange(Math.Abs(bounds.Left - reference.Left), 0, 1.5);
                Assert.InRange(Math.Abs(bounds.Top - reference.Top), 0, 1.5);
                Assert.InRange(Math.Abs(bounds.Right - reference.Right), 0, 1.5);
                Assert.InRange(Math.Abs(bounds.Bottom - reference.Bottom), 0, 1.5);
            }
            else
            {
                expected = bounds;
            }

            var suffix = density.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            using var stream = File.Create(Path.Combine(directory, $"dpi-{suffix}x-preview.png"));
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        }
    }

    private static NormalizedBounds VisibleBounds(SKBitmap bitmap, float density)
    {
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = 0;
        var bottom = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha <= 8)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return new NormalizedBounds(
            left / density,
            top / density,
            right / density,
            bottom / density);
    }

    private readonly record struct NormalizedBounds(
        float Left,
        float Top,
        float Right,
        float Bottom);
}
