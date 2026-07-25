using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Domain.Species;

namespace Honey.Content.WhiteJadeSpider;

internal sealed class WhiteJadeSpiderBehavior(
    string key,
    TimeSpan cooldown,
    Func<PetState, double> score) : IBehaviorDefinition
{
    public BehaviorKey Key { get; } = new(key);

    public TimeSpan Cooldown { get; } = cooldown;

    public double Score(PetState state) => Normalize(score(state));

    private static double Normalize(double value)
    {
        if (!double.IsFinite(value))
        {
            return double.IsPositiveInfinity(value) ? 1 : 0;
        }

        return Math.Clamp(value, 0, 1);
    }
}

internal static class WhiteJadeSpiderBehaviors
{
    public static IReadOnlyList<IBehaviorDefinition> Create() =>
        Array.AsReadOnly<IBehaviorDefinition>(
        [
            new WhiteJadeSpiderBehavior(
                "forage",
                TimeSpan.FromSeconds(10),
                state => 0.15 + (state.Needs.Hunger * 0.75)),
            new WhiteJadeSpiderBehavior(
                "web",
                TimeSpan.FromSeconds(18),
                state => 0.2
                    + (state.Needs.Curiosity * 0.25)
                    + (state.Mode == PetMode.Berserk ? 0.25 : 0)),
            new WhiteJadeSpiderBehavior(
                "play",
                TimeSpan.FromSeconds(8),
                state => 0.15
                    + (state.Needs.Energy * 0.35)
                    + (state.Needs.Curiosity * 0.3)
                    - (state.Needs.Stress * 0.2)),
            new WhiteJadeSpiderBehavior(
                "observe",
                TimeSpan.FromSeconds(3),
                state => 0.35 + (state.Needs.Curiosity * 0.4)),
            new WhiteJadeSpiderBehavior(
                "pounce",
                TimeSpan.FromSeconds(45),
                state => 0.1
                    + (state.Needs.Curiosity * 0.2)
                    + (state.Mode == PetMode.Berserk ? 0.25 : 0)),
            new WhiteJadeSpiderBehavior(
                "groom",
                TimeSpan.FromSeconds(12),
                state => 0.15
                    + (state.Needs.Stress * 0.35)
                    + (state.Needs.Affection * 0.15)),
            new WhiteJadeSpiderBehavior(
                "sleep",
                TimeSpan.FromSeconds(20),
                state => 0.05 + ((1 - state.Needs.Energy) * 0.85))
        ]);
}
