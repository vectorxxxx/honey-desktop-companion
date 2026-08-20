using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderBodyAtlasTests
{
    [Fact]
    public void 四乘四有效流会返回十六个帧()
    {
        using var normal = CreateAtlasStream();
        using var berserk = CreateAtlasStream();
        Assert.True(EmbeddedSpiderBodyAtlas.TryCreate(normal, berserk, out var atlas));
        using var ownedAtlas = Assert.IsType<EmbeddedSpiderBodyAtlas>(atlas);

        for (var index = 0; index < 16; index++)
        {
            Assert.True(ownedAtlas.TryGetFrame(
                PetMode.Normal,
                new SpiderDirection(index, index * MathF.Tau / 16),
                out var frame));
            Assert.Equal(16, frame.Source.Width);
            Assert.Equal(16, frame.Source.Height);
        }
    }

    [Fact]
    public void 非法流会返回失败而不抛出()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);

        var exception = Record.Exception(() =>
        {
            var created = EmbeddedSpiderBodyAtlas.TryCreate(
                stream,
                stream,
                out var atlas);
            Assert.False(created);
            Assert.Null(atlas);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void 非正方形图会被拒绝()
    {
        using var normal = CreatePngStream(63, 64);
        using var berserk = CreatePngStream(64, 64);

        Assert.False(EmbeddedSpiderBodyAtlas.TryCreate(
            normal,
            berserk,
            out var atlas));
        Assert.Null(atlas);
    }

    [Fact]
    public void 不能四等分的正方形图仍会稳定提取左上基准帧()
    {
        using var normal = CreatePngStream(66, 66);
        using var berserk = CreatePngStream(66, 66);
        Assert.True(EmbeddedSpiderBodyAtlas.TryCreate(
            normal,
            berserk,
            out var atlas));
        using var ownedAtlas = Assert.IsType<EmbeddedSpiderBodyAtlas>(atlas);

        Assert.True(ownedAtlas.TryGetFrame(
            PetMode.Normal,
            new SpiderDirection(15, 0),
            out var last));
        Assert.Equal(0, last.Source.Left);
        Assert.Equal(0, last.Source.Top);
        Assert.Equal((int)MathF.Round(66 / 4f), last.Source.Right);
        Assert.Equal((int)MathF.Round(66 / 4f), last.Source.Bottom);
    }

    [Fact]
    public void 默认嵌入资源会返回普通与狂暴十六向帧()
    {
        using var atlas = EmbeddedSpiderBodyAtlas.LoadDefault();

        foreach (var mode in new[] { PetMode.Normal, PetMode.Berserk })
        {
            for (var index = 0; index < 16; index++)
            {
                Assert.True(atlas.TryGetFrame(
                    mode,
                    new SpiderDirection(index, index * MathF.Tau / 16),
                    out var frame));
                Assert.True(frame.Source.Width >= 313);
                Assert.True(frame.Source.Height >= 313);
            }
        }
    }

    [Fact]
    public void 默认图集以腹部为锚点旋转同一精绘基准帧()
    {
        using var atlas = EmbeddedSpiderBodyAtlas.LoadDefault();
        var frames = new List<SpiderAtlasFrame>();
        for (var index = 0; index < SpiderDirection.Count; index++)
        {
            Assert.True(atlas.TryGetFrame(
                PetMode.Normal,
                new SpiderDirection(index, index * MathF.Tau / SpiderDirection.Count),
                out var frame));
            frames.Add(frame);
        }

        Assert.All(frames, frame =>
        {
            Assert.Equal(frames[0].Source, frame.Source);
            Assert.Equal(0.5f, frame.NormalizedAnchor.X, 4);
            Assert.Equal(0.40625f, frame.NormalizedAnchor.Y, 4);
            Assert.False(frame.FlipX);
        });
        var rotation = typeof(SpiderAtlasFrame).GetProperty("RotationRadians");
        Assert.NotNull(rotation);
        Assert.Equal(-MathF.PI, Assert.IsType<float>(rotation.GetValue(frames[0])), 4);
        Assert.Equal(-MathF.PI / 2, Assert.IsType<float>(rotation.GetValue(frames[4])), 4);
        Assert.Equal(0, Assert.IsType<float>(rotation.GetValue(frames[8])), 4);
        Assert.Equal(MathF.PI / 2, Assert.IsType<float>(rotation.GetValue(frames[12])), 4);
    }

    private static MemoryStream CreateAtlasStream() => CreatePngStream(64, 64);

    private static MemoryStream CreatePngStream(int width, int height)
    {
        using var bitmap = new SKBitmap(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    }
}
