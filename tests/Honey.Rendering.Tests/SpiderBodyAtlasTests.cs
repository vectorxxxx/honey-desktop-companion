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
    public void 非正方形或不能四等分的图会被拒绝()
    {
        using var normal = CreatePngStream(63, 64);
        using var berserk = CreatePngStream(64, 64);

        Assert.False(EmbeddedSpiderBodyAtlas.TryCreate(
            normal,
            berserk,
            out var atlas));
        Assert.Null(atlas);
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
