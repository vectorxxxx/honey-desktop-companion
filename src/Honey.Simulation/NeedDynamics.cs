using Honey.Domain.Model;

namespace Honey.Simulation;

public sealed class NeedDynamics
{
    private const double AwakeEnergyChangePerSecond = -0.001;
    private const double SleepingEnergyChangePerSecond = 0.0125;

    public PetNeeds Apply(
        PetNeeds needs,
        TimeSpan elapsed,
        bool isSleeping = false)
    {
        var seconds = elapsed.TotalSeconds;
        var energyChange = isSleeping
            ? SleepingEnergyChangePerSecond
            : AwakeEnergyChangePerSecond;

        return new PetNeeds(
            needs.Hunger + (0.002 * seconds),
            needs.Energy + (energyChange * seconds),
            needs.Curiosity + (0.001 * seconds),
            needs.Affection,
            needs.Stress).Clamp();
    }
}
