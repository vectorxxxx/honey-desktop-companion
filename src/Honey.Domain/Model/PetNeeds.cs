namespace Honey.Domain.Model;

public readonly record struct PetNeeds(
    double Hunger,
    double Energy,
    double Curiosity,
    double Affection,
    double Stress)
{
    public PetNeeds Clamp() => new(
        Normalize(Hunger),
        Normalize(Energy),
        Normalize(Curiosity),
        Normalize(Affection),
        Normalize(Stress));

    private static double Normalize(double value)
    {
        if (!double.IsFinite(value))
        {
            return double.IsPositiveInfinity(value) ? 1 : 0;
        }

        return Math.Clamp(value, 0, 1);
    }
}
