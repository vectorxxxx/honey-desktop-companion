namespace Honey.Domain.Movement;

public sealed record LocomotionFrame(
    LocomotionState State,
    double NormalizedSpeed,
    bool Arrived);
