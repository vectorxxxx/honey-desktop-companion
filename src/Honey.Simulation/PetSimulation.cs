using Honey.Domain.Events;
using Honey.Domain.Model;

namespace Honey.Simulation;

public sealed record SimulationResult(
    PetState State,
    IReadOnlyList<IDomainEvent> Events);

public sealed class PetSimulation
{
    private readonly NeedDynamics needDynamics;
    private readonly ModePolicy modePolicy;

    public PetSimulation()
        : this(new NeedDynamics(), new ModePolicy())
    {
    }

    public PetSimulation(NeedDynamics needDynamics, ModePolicy modePolicy)
    {
        this.needDynamics = needDynamics;
        this.modePolicy = modePolicy;
    }

    public SimulationResult Step(
        PetState state,
        TimeSpan elapsed,
        double random01)
    {
        var snapshot = SimulationSnapshot.Capture(state, elapsed, random01);

        // 随机输入已规范化并进入快照，首版暂不伪造随机行为。
        _ = snapshot.Random01;

        var needs = needDynamics.Apply(snapshot.State.Needs, snapshot.AppliedElapsed);
        var mode = modePolicy.Resolve(snapshot.State.Mode, needs.Stress);
        var events = BuildEvents(snapshot.State, mode);
        var nextState = snapshot.State with
        {
            Needs = needs,
            Mode = mode,
            UpdatedAt = snapshot.State.UpdatedAt + snapshot.AppliedElapsed
        };

        return new SimulationResult(nextState, events);
    }

    private static IReadOnlyList<IDomainEvent> BuildEvents(
        PetState state,
        PetMode nextMode)
    {
        if (state.Mode == nextMode)
        {
            return [];
        }

        return
        [
            new PetModeChanged(state.PetId, state.Mode, nextMode)
        ];
    }
}
