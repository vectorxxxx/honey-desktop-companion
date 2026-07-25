namespace Honey.Domain.Behavior;

public readonly record struct IntentCandidate(
    BehaviorKey Key,
    double Utility,
    TimeSpan CooldownRemaining);
