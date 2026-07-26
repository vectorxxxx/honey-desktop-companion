using Honey.Domain.Activity;
using Honey.Domain.Behavior;

namespace Honey.Desktop.Runtime;

public sealed record ActiveBehaviorState(
    BehaviorKey Behavior,
    string Phase,
    BehaviorOrigin Origin,
    DateTimeOffset StartedAt);
