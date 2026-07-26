namespace Honey.Rendering.Spider;

public static class SpiderArtworkDirectionMap
{
    private static readonly int[] FrameOrder =
    [
        12, 11, 9, 8,
        6, 5, 4, 2,
        0, 1, 3, 7,
        13, 14, 15, 10
    ];

    public static SpiderDirection Map(SpiderDirection logicalDirection)
    {
        var logicalIndex =
            ((logicalDirection.Index % SpiderDirection.Count) + SpiderDirection.Count)
            % SpiderDirection.Count;
        var frameIndex = FrameOrder[logicalIndex];
        return new SpiderDirection(
            frameIndex,
            frameIndex * MathF.Tau / SpiderDirection.Count);
    }
}
