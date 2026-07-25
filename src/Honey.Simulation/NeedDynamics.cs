using Honey.Domain.Model;

namespace Honey.Simulation;

public sealed class NeedDynamics
{
    public PetNeeds Apply(PetNeeds needs, TimeSpan elapsed)
    {
        var seconds = elapsed.TotalSeconds;

        return new PetNeeds(
            needs.Hunger + (0.002 * seconds),
            needs.Energy - (0.001 * seconds),
            needs.Curiosity + (0.001 * seconds),
            needs.Affection,
            needs.Stress).Clamp();
    }
}
