using SkiaSharp;

namespace Honey.Rendering.Spider;

public readonly record struct SpiderAtlasFrame(
    SKBitmap Bitmap,
    SKRectI Source,
    SKPoint NormalizedAnchor,
    bool FlipX = false);
