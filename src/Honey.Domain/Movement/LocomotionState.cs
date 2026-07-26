namespace Honey.Domain.Movement;

public sealed record LocomotionState(
    LocomotionPoint Position,
    LocomotionPoint Velocity,
    LocomotionPoint Facing,
    double StridePhase,
    double TurnLean)
{
    public double Speed => Velocity.Length;

    public static LocomotionState At(LocomotionPoint position) =>
        new(position, LocomotionPoint.Zero, new LocomotionPoint(1, 0), 0, 0);
}
