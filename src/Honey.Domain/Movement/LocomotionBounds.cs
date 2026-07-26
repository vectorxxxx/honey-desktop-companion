namespace Honey.Domain.Movement;

public readonly record struct LocomotionBounds(
    double Left,
    double Top,
    double Right,
    double Bottom)
{
    public LocomotionPoint Clamp(LocomotionPoint point) =>
        new(
            Math.Clamp(point.X, Left, Right),
            Math.Clamp(point.Y, Top, Bottom));

    public bool IsValid =>
        double.IsFinite(Left)
        && double.IsFinite(Top)
        && double.IsFinite(Right)
        && double.IsFinite(Bottom)
        && Right >= Left
        && Bottom >= Top;
}
