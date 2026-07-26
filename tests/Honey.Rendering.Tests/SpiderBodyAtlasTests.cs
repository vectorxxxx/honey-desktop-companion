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
    public void 不能四等分的正方形图会按比例边界完整切片()
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
            new SpiderDirection(0, 0),
            out var bottomRowFrame));
        Assert.Equal(66, bottomRowFrame.Source.Bottom);
        Assert.InRange(bottomRowFrame.Source.Height, 16, 17);
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
    public void 默认素材会把逻辑朝向映射到生成图的半转台并镜像左侧()
    {
        using var atlas = EmbeddedSpiderBodyAtlas.LoadDefault();
        Assert.True(atlas.TryGetFrame(
            PetMode.Normal,
            new SpiderDirection(0, 0),
            out var up));
        Assert.True(atlas.TryGetFrame(
            PetMode.Normal,
            new SpiderDirection(4, MathF.PI / 2),
            out var right));
        Assert.True(atlas.TryGetFrame(
            PetMode.Normal,
            new SpiderDirection(12, MathF.PI * 1.5f),
            out var left));

        Assert.True(up.Source.Top > right.Source.Top);
        Assert.False(right.FlipX);
        Assert.True(left.FlipX);
        Assert.Equal(right.Source, left.Source);
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
