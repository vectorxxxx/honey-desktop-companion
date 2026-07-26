namespace Honey.Rendering.Spider;

public enum SpiderDetailLevel
{
    Compact,
    Standard,
    Showcase
}

public static class SpiderDetailLevelSelector
{
    public const float ReferencePetPixels = 140;

    public static SpiderDetailLevel Select(float displayPixels)
    {
        if (!float.IsFinite(displayPixels) || displayPixels < 90)
        {
            return SpiderDetailLevel.Compact;
        }

        return displayPixels < 180
            ? SpiderDetailLevel.Standard
            : SpiderDetailLevel.Showcase;
    }

    public static SpiderDetailLevel SelectFromPetScale(float petScale) =>
        Select(ReferencePetPixels * petScale);
}
