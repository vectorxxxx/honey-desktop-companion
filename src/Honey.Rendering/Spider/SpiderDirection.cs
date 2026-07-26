namespace Honey.Rendering.Spider;

public readonly record struct SpiderDirection(int Index, float AngleRadians)
{
    public const int Count = 16;
}
