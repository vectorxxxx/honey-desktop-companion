namespace Honey.Domain.Movement;

public readonly record struct LocomotionPoint(double X, double Y)
{
    public static LocomotionPoint Zero => new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public LocomotionPoint Normalize()
    {
        var length = Length;
        return length <= double.Epsilon ? Zero : this / length;
    }

    public static LocomotionPoint operator +(LocomotionPoint left, LocomotionPoint right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static LocomotionPoint operator -(LocomotionPoint left, LocomotionPoint right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static LocomotionPoint operator *(LocomotionPoint point, double scale) =>
        new(point.X * scale, point.Y * scale);

    public static LocomotionPoint operator /(LocomotionPoint point, double scale) =>
        new(point.X / scale, point.Y / scale);

    public static double Dot(LocomotionPoint left, LocomotionPoint right) =>
        left.X * right.X + left.Y * right.Y;
}
