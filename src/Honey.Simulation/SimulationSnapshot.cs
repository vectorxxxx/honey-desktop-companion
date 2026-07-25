using Honey.Domain.Model;

namespace Honey.Simulation;

public sealed record SimulationSnapshot(
    PetState State,
    TimeSpan AppliedElapsed,
    double Random01)
{
    public static SimulationSnapshot Capture(
        PetState state,
        TimeSpan elapsed,
        double random01) =>
        new(
            state,
            TimeSpan.FromSeconds(Math.Clamp(elapsed.TotalSeconds, 0, 1)),
            Math.Clamp(random01, 0, 1));
}
