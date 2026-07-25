using Honey.Domain.Behavior;
using Honey.Domain.Model;

namespace Honey.Domain.Species;

public sealed record SpeciesManifest(
    string SpeciesId,
    Version Version,
    string DisplayName);

public interface IBehaviorDefinition
{
    BehaviorKey Key { get; }

    TimeSpan Cooldown { get; }

    double Score(PetState state);
}

public interface IInteractionRule
{
    bool CanApply(PetState source, PetState target);
}

public interface IProgressionPolicy
{
    int LevelFor(long experience);
}

public sealed class DisabledProgressionPolicy : IProgressionPolicy
{
    public int LevelFor(long experience) => 1;
}

public interface ISpeciesPack
{
    SpeciesManifest Manifest { get; }

    IReadOnlyList<IBehaviorDefinition> Behaviors { get; }

    IReadOnlyList<IInteractionRule> Interactions { get; }

    IProgressionPolicy Progression { get; }
}
