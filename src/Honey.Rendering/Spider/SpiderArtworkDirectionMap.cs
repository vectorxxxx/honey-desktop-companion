namespace Honey.Rendering.Spider;

public static class SpiderArtworkDirectionMap
{
    public static SpiderDirection Map(SpiderDirection logicalDirection)
    {
        var logicalIndex =
            ((logicalDirection.Index % SpiderDirection.Count) + SpiderDirection.Count)
            % SpiderDirection.Count;
        return new SpiderDirection(
            logicalIndex,
            logicalIndex * MathF.Tau / SpiderDirection.Count);
    }
}
