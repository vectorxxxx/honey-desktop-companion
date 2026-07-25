namespace Honey.Domain.Model;

public readonly record struct PetNeeds(
    double Hunger,
    double Energy,
    double Curiosity,
    double Affection,
    double Stress)
{
    public PetNeeds Clamp() => new(
        Math.Clamp(Hunger, 0, 1),
        Math.Clamp(Energy, 0, 1),
        Math.Clamp(Curiosity, 0, 1),
        Math.Clamp(Affection, 0, 1),
        Math.Clamp(Stress, 0, 1));
}
