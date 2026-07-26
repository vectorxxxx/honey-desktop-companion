using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderViewportMetricsTests
{
    [Fact]
    public void ForScale_静止姿态覆盖三档缩放十六方向双模式全部情绪与多时刻()
    {
        foreach (var scale in new[] { 0.4f, 1f, 2f })
        {
            var viewport = SpiderViewportMetrics.ForScale(scale);
            foreach (var mode in Enum.GetValues<PetMode>())
            {
                foreach (var mood in Enum.GetValues<PetMood>())
                {
                    foreach (var time in new[] { 0d, 0.31, 1.17, 3.9 })
                    {
                        for (var directionIndex = 0;
                            directionIndex < SpiderDirection.Count;
                            directionIndex++)
                        {
                            var angle =
                                directionIndex * MathF.Tau
                                / SpiderDirection.Count;
                            var snapshot = new RenderSnapshot(
                                mode,
                                mood,
                                0,
                                0,
                                time,
                                scale,
                                string.Empty,
                                FacingX: MathF.Sin(angle),
                                FacingY: -MathF.Cos(angle));
                            var pose = SpiderGeometry.CreatePose(
                                viewport.Width,
                                viewport.Height,
                                snapshot);

                            AssertPoseInsideViewport(
                                pose,
                                viewport,
                                8,
                                $"静止 scale={scale}, mode={mode}, mood={mood}, time={time}, direction={directionIndex}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ForScale_最大移动幅度覆盖十六方向双模式与完整步相()
    {
        foreach (var scale in new[] { 1f, 2f })
        {
            var viewport = SpiderViewportMetrics.ForScale(scale);
            foreach (var mode in Enum.GetValues<PetMode>())
            {
                foreach (var mood in new[] { PetMood.Alert, PetMood.Angry })
                {
                    foreach (var stridePhase in new[]
                    {
                        0f, 0.125f, 0.25f, 0.375f, 0.5f,
                        0.625f, 0.75f, 0.875f, 1f
                    })
                    {
                        for (var directionIndex = 0;
                            directionIndex < SpiderDirection.Count;
                            directionIndex++)
                        {
                            var angle =
                                directionIndex * MathF.Tau
                                / SpiderDirection.Count;
                            var snapshot = new RenderSnapshot(
                                mode,
                                mood,
                                0,
                                0,
                                0,
                                scale,
                                string.Empty,
                                FacingX: MathF.Sin(angle),
                                FacingY: -MathF.Cos(angle),
                                NormalizedSpeed: 1,
                                StridePhase: stridePhase);
                            var pose = SpiderGeometry.CreatePose(
                                viewport.Width,
                                viewport.Height,
                                snapshot);

                            AssertPoseInsideViewport(
                                pose,
                                viewport,
                                8,
                                $"移动 scale={scale}, mode={mode}, mood={mood}, phase={stridePhase}, direction={directionIndex}");
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(0.6f)]
    [InlineData(1.0f)]
    [InlineData(1.6f)]
    [InlineData(2.0f)]
    public void ForScale_动态姿态与描边始终位于视口内(float scale)
    {
        var viewport = SpiderViewportMetrics.ForScale(scale);
        foreach (var mood in Enum.GetValues<PetMood>())
        {
            foreach (var time in new[] { 0d, 0.25, 0.75, 1.25 })
            {
                var snapshot = new RenderSnapshot(
                    PetMode.Normal,
                    mood,
                    0,
                    0,
                    time,
                    scale,
                    "observe");
                var pose = SpiderGeometry.CreatePose(viewport.Width, viewport.Height, snapshot);

                Assert.True(pose.ContentBounds.Left >= 0);
                Assert.True(pose.ContentBounds.Top >= 0);
                Assert.True(pose.ContentBounds.Right <= viewport.Width);
                Assert.True(pose.ContentBounds.Bottom <= viewport.Height);
            }
        }
    }

    [Fact]
    public void Draw_二倍缩放主体像素不接触边缘并输出预览()
    {
        var viewport = SpiderViewportMetrics.ForScale(2);
        var snapshot = new RenderSnapshot(
            PetMode.Normal,
            PetMood.Alert,
            0,
            0,
            0.75,
            2,
            "observe");
        using var bitmap = new SKBitmap(
            (int)viewport.Width,
            (int)viewport.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        using (var scene = new WhiteJadeSpiderScene())
        {
            canvas.Clear(SKColors.Transparent);
            scene.Draw(canvas, snapshot, viewport.Width, viewport.Height);
        }

        for (var x = 0; x < bitmap.Width; x++)
        {
            Assert.Equal((byte)0, bitmap.GetPixel(x, 0).Alpha);
            Assert.Equal((byte)0, bitmap.GetPixel(x, bitmap.Height - 1).Alpha);
        }

        for (var y = 0; y < bitmap.Height; y++)
        {
            Assert.Equal((byte)0, bitmap.GetPixel(0, y).Alpha);
            Assert.Equal((byte)0, bitmap.GetPixel(bitmap.Width - 1, y).Alpha);
        }

        var directory = Path.Combine(Path.GetTempPath(), "honey-task6-preview");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, "scale2-preview.png"));
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
    }

    [Fact]
    public void CreatePose_二倍缩放的主体几何宽度约为一倍的两倍()
    {
        var normalViewport = SpiderViewportMetrics.ForScale(1);
        var largeViewport = SpiderViewportMetrics.ForScale(2);
        var normal = SpiderGeometry.CreatePose(
            normalViewport.Width,
            normalViewport.Height,
            new RenderSnapshot(PetMode.Normal, PetMood.Curious, 0, 0, 0, 1, "observe"));
        var large = SpiderGeometry.CreatePose(
            largeViewport.Width,
            largeViewport.Height,
            new RenderSnapshot(PetMode.Normal, PetMood.Curious, 0, 0, 0, 2, "observe"));

        Assert.InRange(
            large.ContentBounds.Width / normal.ContentBounds.Width,
            1.99,
            2.01);
    }

    [Fact]
    public void Dispose_重复调用安全且释放后拒绝绘制()
    {
        var scene = new WhiteJadeSpiderScene();
        scene.Dispose();
        scene.Dispose();
        using var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);

        Assert.Throws<ObjectDisposedException>(() =>
            scene.Draw(
                canvas,
                new RenderSnapshot(PetMode.Normal, PetMood.Curious, 0, 0, 0, 1, "observe")));
    }

    private static void AssertPoseInsideViewport(
        SpiderPose pose,
        SpiderViewportSize viewport,
        float safePadding,
        string context)
    {
        Assert.True(
            pose.ContentBounds.Left >= safePadding,
            $"{context}: 左侧仅余 {pose.ContentBounds.Left:F2}px。");
        Assert.True(
            pose.ContentBounds.Top >= safePadding,
            $"{context}: 顶部仅余 {pose.ContentBounds.Top:F2}px。");
        Assert.True(
            pose.ContentBounds.Right <= viewport.Width - safePadding,
            $"{context}: 右侧仅余 {viewport.Width - pose.ContentBounds.Right:F2}px。");
        Assert.True(
            pose.ContentBounds.Bottom <= viewport.Height - safePadding,
            $"{context}: 底部仅余 {viewport.Height - pose.ContentBounds.Bottom:F2}px。");
    }
}
