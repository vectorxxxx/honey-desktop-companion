namespace Honey.Domain.Movement;

public readonly record struct PointerStimulus(
    double Distance,
    double Speed,
    double InterestSeconds,
    TimeSpan ChaseCooldownRemaining)
{
    public static PointerStimulus None =>
        new(double.PositiveInfinity, 0, 0, TimeSpan.Zero);
}
