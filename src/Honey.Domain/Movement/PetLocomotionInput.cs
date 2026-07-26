namespace Honey.Domain.Movement;

public sealed record PetLocomotionInput(
    LocomotionIntent Intent,
    LocomotionPoint Target,
    LocomotionBounds Bounds,
    double SpeedMultiplier = 1,
    bool IsBerserk = false);
