namespace Honey.Domain.Behavior;

public readonly record struct BehaviorKey(string Value)
{
    public override string ToString() => Value;
}
