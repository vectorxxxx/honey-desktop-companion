namespace Honey.Rendering.Spider;

public sealed class SpiderDirectionSelector
{
    private readonly float _hysteresis;
    private int? _current;

    public SpiderDirectionSelector(float hysteresisDegrees = 4)
    {
        if (!float.IsFinite(hysteresisDegrees) || hysteresisDegrees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees));
        }

        _hysteresis = MathF.Min(hysteresisDegrees, 10) * MathF.PI / 180;
    }

    public SpiderDirection Select(float facingX, float facingY)
    {
        if (!float.IsFinite(facingX)
            || !float.IsFinite(facingY)
            || facingX * facingX + facingY * facingY <= float.Epsilon)
        {
            return Direction(_current ?? 0);
        }

        var angle = MathF.Atan2(facingX, -facingY);
        if (angle < 0)
        {
            angle += MathF.Tau;
        }

        var sector = MathF.Tau / SpiderDirection.Count;
        var candidate = (int)MathF.Round(angle / sector) % SpiderDirection.Count;
        if (_current is { } current)
        {
            var center = current * sector;
            var delta = CircularDistance(angle, center);
            if (delta <= sector / 2 + _hysteresis)
            {
                candidate = current;
            }
        }

        _current = candidate;
        return Direction(candidate);
    }

    private static SpiderDirection Direction(int index) =>
        new(index, index * MathF.Tau / SpiderDirection.Count);

    private static float CircularDistance(float left, float right)
    {
        var delta = MathF.Abs(left - right) % MathF.Tau;
        return MathF.Min(delta, MathF.Tau - delta);
    }
}
