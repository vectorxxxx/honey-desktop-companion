namespace Honey.Domain.Movement;

public sealed record PetLocomotionProfile(
    double MaxSpeed,
    double Acceleration,
    double DecelerationRadius,
    double ArrivalRadius,
    double MaxTurnRadiansPerSecond,
    double BerserkSpeedMultiplier,
    TimeSpan MaximumStep)
{
    public PetLocomotionProfile Normalize() =>
        this with
        {
            MaxSpeed = Math.Max(1, MaxSpeed),
            Acceleration = Math.Max(1, Acceleration),
            DecelerationRadius = Math.Max(1, DecelerationRadius),
            ArrivalRadius = Math.Max(0, ArrivalRadius),
            MaxTurnRadiansPerSecond = Math.Max(0.01, MaxTurnRadiansPerSecond),
            BerserkSpeedMultiplier = Math.Max(1, BerserkSpeedMultiplier),
            MaximumStep = MaximumStep <= TimeSpan.Zero
                ? TimeSpan.FromMilliseconds(100)
                : MaximumStep
        };
}
