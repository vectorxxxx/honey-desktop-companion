using Honey.Domain.Model;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public readonly record struct SpiderMaterialPalette(
    SKColor LegShadow,
    SKColor LegSurface,
    SKColor BodyEdge,
    SKColor BodyMiddle,
    SKColor BodyHighlight,
    SKColor InternalGlow,
    SKColor Vein,
    SKColor Particle,
    SKColor Eye)
{
    public static SpiderMaterialPalette For(PetMode mode) =>
        mode == PetMode.Berserk
            ? new SpiderMaterialPalette(
                new SKColor(67, 6, 14, 225),
                new SKColor(205, 44, 51, 245),
                new SKColor(69, 4, 13, 252),
                new SKColor(202, 35, 46, 250),
                new SKColor(255, 188, 166, 250),
                new SKColor(255, 52, 37, 205),
                new SKColor(255, 69, 47, 190),
                new SKColor(244, 42, 39, 145),
                new SKColor(255, 45, 34, 255))
            : new SpiderMaterialPalette(
                new SKColor(67, 80, 88, 205),
                new SKColor(222, 231, 235, 248),
                new SKColor(91, 105, 114, 242),
                new SKColor(220, 227, 232, 250),
                new SKColor(255, 255, 255, 252),
                new SKColor(178, 205, 219, 38),
                new SKColor(135, 158, 170, 72),
                new SKColor(211, 228, 237, 82),
                new SKColor(55, 68, 75, 255));
}
