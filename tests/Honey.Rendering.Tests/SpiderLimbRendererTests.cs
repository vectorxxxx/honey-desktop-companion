using System.Collections.Concurrent;
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

    [Fact]
    public void DrawOuterSegments_关节采用入射与出射方向的角平分切线()
    {
        using var renderer = new SpiderLimbRenderer();
        var palette = SpiderMaterialPalette.For(PetMode.Normal);
        var leg = RightAngleLeg();
        using var actual = RenderBitmap(200, 140, canvas =>
            renderer.DrawOuterSegments(canvas, leg, palette, SpiderDetailLevel.Standard));
        using var expected = RenderBitmap(200, 140, canvas =>
        {
            var hipWidth = leg.Width * 0.76f;
            var kneeWidth = leg.Width * 0.52f;
            renderer.DrawSegment(
                canvas,
                SpiderLimbGeometry.Create(leg.Hip, leg.Knee, hipWidth, kneeWidth),
                palette,
                SpiderDetailLevel.Standard);
            renderer.DrawSegment(
                canvas,
                SpiderLimbGeometry.Create(leg.Knee, leg.Tip, kneeWidth, leg.Width * 0.20f),
                palette,
                SpiderDetailLevel.Standard);
            renderer.DrawJoint(
                canvas,
                leg.Hip,
                BisectorAngle(leg.Root, leg.Hip, leg.Knee),
                hipWidth,
                palette,
                SpiderDetailLevel.Standard);
            renderer.DrawJoint(
                canvas,
                leg.Knee,
                BisectorAngle(leg.Hip, leg.Knee, leg.Tip),
                kneeWidth,
                palette,
                SpiderDetailLevel.Standard);
        });

        Assert.Equal(0, CountDifferentPixels(actual, expected));
    }

    [Fact]
    public void DrawJoint_同一椭圆翻转半周仍保持屏幕左上世界光()
    {
        using var renderer = new SpiderLimbRenderer();
        var palette = SpiderMaterialPalette.For(PetMode.Normal);
        using var forward = RenderBitmap(120, 120, canvas =>
            renderer.DrawJoint(
                canvas,
                new SKPoint(60, 60),
                0,
                40,
                palette,
                SpiderDetailLevel.Standard));
        using var reversed = RenderBitmap(120, 120, canvas =>
            renderer.DrawJoint(
                canvas,
                new SKPoint(60, 60),
                MathF.PI,
                40,
                palette,
                SpiderDetailLevel.Standard));
        using var quarterTurn = RenderBitmap(120, 120, canvas =>
            renderer.DrawJoint(
                canvas,
                new SKPoint(60, 60),
                MathF.PI / 2,
                40,
                palette,
                SpiderDetailLevel.Standard));

        Assert.InRange(CountDifferentPixels(forward, reversed), 0, 10);
        Assert.True(
            RegionBrightness(forward, 38, 38, 60, 60)
                > RegionBrightness(forward, 60, 60, 82, 82));
        Assert.True(
            RegionBrightness(reversed, 38, 38, 60, 60)
                > RegionBrightness(reversed, 60, 60, 82, 82));
        var quarterTurnTopLeft = RegionBrightness(quarterTurn, 48, 42, 60, 60);
        Assert.True(quarterTurnTopLeft > RegionBrightness(quarterTurn, 60, 42, 72, 60));
        Assert.True(quarterTurnTopLeft > RegionBrightness(quarterTurn, 48, 60, 60, 78));
        Assert.True(quarterTurnTopLeft > RegionBrightness(quarterTurn, 60, 60, 72, 78));
    }

    [Fact]
    public void DrawSegment_紧凑层使用纯色并与标准层有差异()
    {
        using var renderer = new SpiderLimbRenderer();
        using var compact = RenderDetail(renderer, SpiderDetailLevel.Compact);
        using var standard = RenderDetail(renderer, SpiderDetailLevel.Standard);

        Assert.Equal(compact.GetPixel(60, 60), compact.GetPixel(160, 60));
        Assert.True(CountDifferentPixels(compact, standard) > 100);
    }

    [Fact]
    public void DrawJoint_紧凑层以低对比暗边区分关节与腿段()
    {
        using var renderer = new SpiderLimbRenderer();
        var palette = SpiderMaterialPalette.For(PetMode.Normal);
        using var bitmap = RenderBitmap(120, 120, canvas =>
            renderer.DrawJoint(
                canvas,
                new SKPoint(60, 60),
                0,
                40,
                palette,
                SpiderDetailLevel.Compact));

        var center = bitmap.GetPixel(60, 60);
        var leftEdge = bitmap.GetPixel(40, 60);

        Assert.InRange(ColorDistance(center, palette.LegSurface), 0, 8);
        Assert.True(leftEdge.Alpha >= 128);
        Assert.True(
            ColorDistance(leftEdge, palette.LegSurface) >= 20,
            "紧凑关节边缘必须区别于单层玉色中心，以便与腿段分辨。");
    }

    [Fact]
    public void DrawJoint_紧凑层在最小尺寸保留可辨识暗边()
    {
        using var renderer = new SpiderLimbRenderer();
        var layout = SpiderLayout.Create(320, 320, 60f / 140f);
        var leg = layout.Legs[1];
        var widths = new[]
        {
            ("髋关节", leg.Width * 0.76f),
            ("膝关节", leg.Width * 0.52f)
        };
        var modes = new[]
        {
            (PetMode.Normal, "普通态"),
            (PetMode.Berserk, "狂暴态")
        };

        foreach (var mode in modes)
        {
            var palette = SpiderMaterialPalette.For(mode.Item1);
            foreach (var joint in widths)
            {
                var center = new SKPoint(73.5f, 71.5f);
                using var bitmap = RenderBitmap(160, 160, canvas =>
                    renderer.DrawJoint(
                        canvas,
                        center,
                        MathF.PI / 5,
                        joint.Item2,
                        palette,
                        SpiderDetailLevel.Compact));

                var pixels = InspectCompactJointPixels(
                    bitmap,
                    center,
                    MathF.PI / 5,
                    joint.Item2,
                    palette.LegSurface);
                var description = $"{mode.Item2}{joint.Item1}宽度 {joint.Item2:F2}";
                var nearestCenterPixel = bitmap.GetPixel((int)center.X, (int)center.Y);
                var minimumDarkEdgeCount = joint.Item1 == "髋关节" ? 3 : 2;
                Assert.True(
                    pixels.CenterSurfaceCount >= 1,
                    $"{description} 中心玉色像素不足：实际 {pixels.CenterSurfaceCount}，最近像素 {nearestCenterPixel}，颜色距离 {ColorDistance(nearestCenterPixel, palette.LegSurface):F2}。");
                Assert.True(
                    pixels.DarkEdgeCount >= minimumDarkEdgeCount,
                    $"{description} 暗边像素不足：实际 {pixels.DarkEdgeCount}，要求至少 {minimumDarkEdgeCount} 个。");
            }
        }
    }

    [Fact]
    public void DrawOuterSegments_最小尺寸紧凑层在髋膝区域增加关节轮廓()
    {
        using var renderer = new SpiderLimbRenderer();
        var layout = SpiderLayout.Create(320, 320, 60f / 140f);
        var leg = layout.Legs[1];
        var palette = SpiderMaterialPalette.For(PetMode.Normal);
        using var segmentsOnly = RenderBitmap(320, 320, canvas =>
        {
            renderer.DrawSegment(
                canvas,
                SpiderLimbGeometry.Create(
                    leg.Hip,
                    leg.Knee,
                    leg.Width * 0.76f,
                    leg.Width * 0.52f),
                palette,
                SpiderDetailLevel.Compact);
            renderer.DrawSegment(
                canvas,
                SpiderLimbGeometry.Create(
                    leg.Knee,
                    leg.Tip,
                    leg.Width * 0.52f,
                    leg.Width * 0.20f),
                palette,
                SpiderDetailLevel.Compact);
        });
        using var withJoints = RenderBitmap(320, 320, canvas =>
            renderer.DrawOuterSegments(canvas, leg, palette, SpiderDetailLevel.Compact));

        var radius = Math.Max(4f, leg.Width);
        var hipDifference = CountDifferentPixelsNear(segmentsOnly, withJoints, leg.Hip, radius);
        var kneeDifference = CountDifferentPixelsNear(segmentsOnly, withJoints, leg.Knee, radius);

        Assert.True(
            hipDifference >= 3,
            $"最小尺寸髋关节区域新增轮廓像素不足：实际 {hipDifference}，要求至少 3 个。");
        Assert.True(
            kneeDifference >= 3,
            $"最小尺寸膝关节区域新增轮廓像素不足：实际 {kneeDifference}，要求至少 3 个。");
    }

    [Fact]
    public void DrawSegment_展示层具有独立克制刻线并与标准层有差异()
    {
        using var renderer = new SpiderLimbRenderer();
        using var standard = RenderDetail(renderer, SpiderDetailLevel.Standard);
        using var showcase = RenderDetail(renderer, SpiderDetailLevel.Showcase);

        Assert.True(CountDifferentPixels(standard, showcase) > 20);
    }

    [Fact]
    public void DrawSegment_轻弧让等宽段中部略宽于近端()
    {
        using var renderer = new SpiderLimbRenderer();
        using var bitmap = RenderUniformSegment(
            renderer,
            new SKPoint(25, 60),
            new SKPoint(195, 60));

        Assert.True(VerticalOpaqueSpan(bitmap, 110) > VerticalOpaqueSpan(bitmap, 35));
    }

    [Fact]
    public void DrawCompleteLeg_三段接缝保持连续覆盖()
    {
        using var renderer = new SpiderLimbRenderer();
        var leg = RightAngleLeg();
        using var bitmap = RenderBitmap(200, 140, canvas =>
            renderer.DrawCompleteLeg(
                canvas,
                leg,
                SpiderMaterialPalette.For(PetMode.Normal),
                SpiderDetailLevel.Standard));

        Assert.True(HasOpaquePixel(bitmap, leg.Hip, 2));
        Assert.True(HasOpaquePixel(bitmap, leg.Knee, 2));
        AssertSegmentCovered(bitmap, leg.Root, leg.Hip);
        AssertSegmentCovered(bitmap, leg.Hip, leg.Knee);
        AssertSegmentCovered(bitmap, leg.Knee, leg.Tip);
    }

    [Fact]
    public void DrawSegment_伪造非法段与极大关节宽度不抛出也不污染像素()
    {
        using var renderer = new SpiderLimbRenderer();
        using var bitmap = new SKBitmap(120, 120, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var palette = SpiderMaterialPalette.For(PetMode.Normal);
        var invalidCoordinate = new SpiderLimbSegment(
            new SKPoint(20, 60),
            new SKPoint(100, 60),
            new SKPoint(float.NaN, 50),
            new SKPoint(20, 70),
            new SKPoint(100, 58),
            new SKPoint(100, 62),
            20,
            4,
            0,
            true);
        var overflowingDerivedValue = new SpiderLimbSegment(
            new SKPoint(float.MaxValue, 60),
            new SKPoint(-float.MaxValue, 60),
            new SKPoint(20, 50),
            new SKPoint(20, 70),
            new SKPoint(100, 58),
            new SKPoint(100, 62),
            float.MaxValue,
            float.MaxValue,
            0,
            true);

        var exception = Record.Exception(() =>
        {
            renderer.DrawSegment(
                canvas,
                invalidCoordinate,
                palette,
                SpiderDetailLevel.Standard);
            renderer.DrawSegment(
                canvas,
                overflowingDerivedValue,
                palette,
                SpiderDetailLevel.Standard);
            renderer.DrawJoint(
                canvas,
                new SKPoint(60, 60),
                0,
                float.MaxValue,
                palette,
                SpiderDetailLevel.Standard);
        });

        Assert.Null(exception);
        Assert.Equal(0, CountOpaque(bitmap));
    }

    [Fact]
    public void DrawJoint_有效与非法输入都恢复Canvas保存层级()
    {
        using var renderer = new SpiderLimbRenderer();
        using var bitmap = new SKBitmap(120, 120);
        using var canvas = new SKCanvas(bitmap);
        var initialSaveCount = canvas.SaveCount;
        var palette = SpiderMaterialPalette.For(PetMode.Normal);

        renderer.DrawJoint(
            canvas,
            new SKPoint(60, 60),
            MathF.PI / 3,
            32,
            palette,
            SpiderDetailLevel.Showcase);
        Assert.Equal(initialSaveCount, canvas.SaveCount);

        renderer.DrawJoint(
            canvas,
            new SKPoint(60, 60),
            float.NaN,
            float.MaxValue,
            palette,
            SpiderDetailLevel.Showcase);
        Assert.Equal(initialSaveCount, canvas.SaveCount);
    }

    [Fact]
    public async Task DrawCompleteLeg_与Dispose并发时只允许完成或对象已释放异常()
    {
        var renderer = new SpiderLimbRenderer();
        var unexpected = new ConcurrentQueue<Exception>();
        using var start = new ManualResetEventSlim();
        using var firstDrawCompleted = new ManualResetEventSlim();
        var leg = RightAngleLeg();
        var palette = SpiderMaterialPalette.For(PetMode.Normal);
        var cancellationToken = TestContext.Current.CancellationToken;
        var completedDraws = 0;
        var drawers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                using var bitmap = new SKBitmap(200, 140);
                using var canvas = new SKCanvas(bitmap);
                start.Wait(cancellationToken);
                for (var iteration = 0; iteration < 80; iteration++)
                {
                    try
                    {
                        renderer.DrawCompleteLeg(
                            canvas,
                            leg,
                            palette,
                            SpiderDetailLevel.Showcase);
                        Interlocked.Increment(ref completedDraws);
                        firstDrawCompleted.Set();
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        unexpected.Enqueue(exception);
                        return;
                    }
                }
            }, cancellationToken))
            .ToArray();
        var disposer = Task.Run(() =>
        {
            start.Wait(cancellationToken);
            firstDrawCompleted.Wait(cancellationToken);
            Thread.Yield();
            renderer.Dispose();
        }, cancellationToken);

        start.Set();
        await Task.WhenAll([.. drawers, disposer]);
        renderer.Dispose();

        Assert.True(Volatile.Read(ref completedDraws) > 0);
        Assert.Empty(unexpected);
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

    private static SKBitmap RenderDetail(
        SpiderLimbRenderer renderer,
        SpiderDetailLevel detailLevel) =>
        RenderBitmap(220, 120, canvas =>
            renderer.DrawSegment(
                canvas,
                SpiderLimbGeometry.Create(
                    new SKPoint(25, 60),
                    new SKPoint(195, 60),
                    24,
                    24),
                SpiderMaterialPalette.For(PetMode.Normal),
                detailLevel));

    private static SKBitmap RenderBitmap(
        int width,
        int height,
        Action<SKCanvas> draw)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        draw(canvas);
        return bitmap;
    }

    private static SpiderLeg RightAngleLeg() =>
        new(
            new SKPoint(30, 100),
            new SKPoint(90, 100),
            new SKPoint(90, 40),
            new SKPoint(150, 40),
            24,
            SpiderLegLayer.AboveBody);

    private static float BisectorAngle(SKPoint previous, SKPoint center, SKPoint next)
    {
        var incoming = Unit(center.X - previous.X, center.Y - previous.Y);
        var outgoing = Unit(next.X - center.X, next.Y - center.Y);
        var x = incoming.X + outgoing.X;
        var y = incoming.Y + outgoing.Y;
        if (x * x + y * y <= 0.000001f)
        {
            return MathF.Atan2(outgoing.Y, outgoing.X);
        }

        return MathF.Atan2(y, x);
    }

    private static SKPoint Unit(float x, float y)
    {
        var length = MathF.Sqrt(x * x + y * y);
        return new SKPoint(x / length, y / length);
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

    private static double RegionBrightness(
        SKBitmap bitmap,
        int left,
        int top,
        int right,
        int bottom)
    {
        var sum = 0d;
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha <= 8)
                {
                    continue;
                }

                sum += pixel.Red * 0.2126 + pixel.Green * 0.7152 + pixel.Blue * 0.0722;
                count++;
            }
        }

        return count == 0 ? 0 : sum / count;
    }

    private static bool HasOpaquePixel(SKBitmap bitmap, SKPoint center, int radius)
    {
        for (var y = (int)center.Y - radius; y <= (int)center.Y + radius; y++)
        {
            for (var x = (int)center.X - radius; x <= (int)center.X + radius; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 8)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AssertSegmentCovered(SKBitmap bitmap, SKPoint start, SKPoint end)
    {
        for (var step = 0; step <= 20; step++)
        {
            var amount = step / 20f;
            var point = new SKPoint(
                start.X + (end.X - start.X) * amount,
                start.Y + (end.Y - start.Y) * amount);
            Assert.True(
                HasOpaquePixel(bitmap, point, 1),
                $"三段接缝在 ({point.X}, {point.Y}) 附近出现透明断点。");
        }
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

    private static CompactJointPixelStatistics InspectCompactJointPixels(
        SKBitmap bitmap,
        SKPoint center,
        float angleRadians,
        float width,
        SKColor surface)
    {
        var halfWidth = width * 1.05f / 2;
        var halfHeight = width * 0.78f / 2;
        var extent = MathF.Max(halfWidth, halfHeight) + 1.5f;
        var cosine = MathF.Cos(angleRadians);
        var sine = MathF.Sin(angleRadians);
        var centerSurfaceCount = 0;
        var darkEdgeCount = 0;
        for (var y = Math.Max(0, (int)MathF.Floor(center.Y - extent));
             y <= Math.Min(bitmap.Height - 1, (int)MathF.Ceiling(center.Y + extent));
             y++)
        {
            for (var x = Math.Max(0, (int)MathF.Floor(center.X - extent));
                 x <= Math.Min(bitmap.Width - 1, (int)MathF.Ceiling(center.X + extent));
                 x++)
            {
                var offsetX = x - center.X;
                var offsetY = y - center.Y;
                var localX = cosine * offsetX + sine * offsetY;
                var localY = -sine * offsetX + cosine * offsetY;
                var radius = MathF.Sqrt(
                    localX * localX / (halfWidth * halfWidth)
                    + localY * localY / (halfHeight * halfHeight));
                var pixel = bitmap.GetPixel(x, y);
                if (radius <= 0.60f
                    && pixel.Alpha >= 128
                    && ColorDistance(pixel, surface) <= 8)
                {
                    centerSurfaceCount++;
                }

                if (radius >= 0.65f
                    && radius <= 1.35f
                    && pixel.Alpha >= 128
                    && ColorDistance(pixel, surface) >= 20)
                {
                    darkEdgeCount++;
                }
            }
        }

        return new CompactJointPixelStatistics(centerSurfaceCount, darkEdgeCount);
    }

    private static int CountDifferentPixelsNear(
        SKBitmap left,
        SKBitmap right,
        SKPoint center,
        float radius)
    {
        var count = 0;
        var squaredRadius = radius * radius;
        for (var y = Math.Max(0, (int)MathF.Floor(center.Y - radius));
             y <= Math.Min(left.Height - 1, (int)MathF.Ceiling(center.Y + radius));
             y++)
        {
            for (var x = Math.Max(0, (int)MathF.Floor(center.X - radius));
                 x <= Math.Min(left.Width - 1, (int)MathF.Ceiling(center.X + radius));
                 x++)
            {
                var offsetX = x - center.X;
                var offsetY = y - center.Y;
                if (offsetX * offsetX + offsetY * offsetY <= squaredRadius
                    && left.GetPixel(x, y) != right.GetPixel(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static double ColorDistance(SKColor left, SKColor right)
    {
        var red = left.Red - right.Red;
        var green = left.Green - right.Green;
        var blue = left.Blue - right.Blue;
        return Math.Sqrt(red * red + green * green + blue * blue);
    }

    private readonly record struct CompactJointPixelStatistics(
        int CenterSurfaceCount,
        int DarkEdgeCount);
}
