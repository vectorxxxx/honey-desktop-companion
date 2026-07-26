using Honey.Domain.Model;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed class EmbeddedSpiderBodyAtlas : ISpiderBodyAtlas
{
    public const string NormalResourceName =
        "Honey.Rendering.Assets.white-jade-spider-normal-atlas.png";
    public const string BerserkResourceName =
        "Honey.Rendering.Assets.white-jade-spider-berserk-atlas.png";

    private readonly SKBitmap _normal;
    private readonly SKBitmap _berserk;
    private int _disposed;

    private EmbeddedSpiderBodyAtlas(SKBitmap normal, SKBitmap berserk)
    {
        _normal = normal;
        _berserk = berserk;
    }

    public static bool TryCreate(
        Stream normalStream,
        Stream berserkStream,
        out EmbeddedSpiderBodyAtlas? atlas)
    {
        atlas = null;
        if (normalStream is null || berserkStream is null)
        {
            return false;
        }

        SKBitmap? normal = null;
        SKBitmap? berserk = null;
        try
        {
            normal = SKBitmap.Decode(normalStream);
            berserk = SKBitmap.Decode(berserkStream);
            if (!IsValid(normal)
                || !IsValid(berserk)
                || normal.Width != berserk.Width
                || normal.Height != berserk.Height)
            {
                normal?.Dispose();
                berserk?.Dispose();
                return false;
            }

            atlas = new EmbeddedSpiderBodyAtlas(normal, berserk);
            return true;
        }
        catch
        {
            normal?.Dispose();
            berserk?.Dispose();
            return false;
        }
    }

    public static bool TryLoadDefault(out EmbeddedSpiderBodyAtlas? atlas)
    {
        atlas = null;
        var assembly = typeof(EmbeddedSpiderBodyAtlas).Assembly;
        using var normal = assembly.GetManifestResourceStream(NormalResourceName);
        using var berserk = assembly.GetManifestResourceStream(BerserkResourceName);
        return normal is not null
            && berserk is not null
            && TryCreate(normal, berserk, out atlas);
    }

    public static EmbeddedSpiderBodyAtlas LoadDefault() =>
        TryLoadDefault(out var atlas)
            ? atlas!
            : throw new InvalidOperationException(
                $"无法加载嵌入图集 {NormalResourceName} 与 {BerserkResourceName}。");

    public bool TryGetFrame(
        PetMode mode,
        SpiderDirection direction,
        out SpiderAtlasFrame frame)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            frame = default;
            return false;
        }

        var bitmap = mode == PetMode.Berserk ? _berserk : _normal;
        var index = ((direction.Index % SpiderDirection.Count) + SpiderDirection.Count)
            % SpiderDirection.Count;
        var column = index % 4;
        var row = index / 4;
        var left = (int)MathF.Round(column * bitmap.Width / 4f);
        var top = (int)MathF.Round(row * bitmap.Height / 4f);
        var right = (int)MathF.Round((column + 1) * bitmap.Width / 4f);
        var bottom = (int)MathF.Round((row + 1) * bitmap.Height / 4f);
        frame = new SpiderAtlasFrame(
            bitmap,
            SKRectI.Create(left, top, right - left, bottom - top),
            new SKPoint(0.5f, 0.54f));
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _normal.Dispose();
        _berserk.Dispose();
    }

    private static bool IsValid(SKBitmap? bitmap) =>
        bitmap is not null
        && bitmap.Width > 0
        && bitmap.Width == bitmap.Height;
}
