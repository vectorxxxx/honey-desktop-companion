using Honey.Domain.Behavior;

namespace Honey.Domain.Activity;

public sealed record PetActivityEntry(
    DateTimeOffset At,
    BehaviorKey Behavior,
    BehaviorOrigin Origin,
    PetActivityOutcome Outcome,
    string? Detail = null);
