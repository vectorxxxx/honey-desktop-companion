using Honey.Desktop.Runtime;
using Honey.Domain.Activity;
using Honey.Domain.Model;

namespace Honey.Desktop.Status;

public static class PetStatusProjector
{
    public static PetStatusSnapshot Project(
        PetState state,
        ActiveBehaviorState active,
        IReadOnlyList<PetActivityEntry> entries,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(entries);
        var needs = state.Needs.Clamp();
        return new PetStatusSnapshot(
            state.PetId,
            state.SpeciesId,
            "小玉",
            state.Mood,
            state.Mode,
            [
                Gauge("hunger", "饥饿", needs.Hunger, false, "越高越需要进食"),
                Gauge("energy", "精力", needs.Energy, true, "越高状态越充足"),
                Gauge("curiosity", "好奇", needs.Curiosity, true, "越高越倾向探索和玩耍"),
                Gauge("affection", "亲密", needs.Affection, true, "越高越信任用户"),
                Gauge("stress", "压力", needs.Stress, false, "越高越警觉")
            ],
            active.Behavior.Value,
            active.Phase,
            active.Origin,
            now > active.StartedAt ? now - active.StartedAt : TimeSpan.Zero,
            entries.Take(6).ToArray());
    }

    private static PetNeedGauge Gauge(
        string key,
        string name,
        double raw,
        bool highIsGood,
        string description) =>
        new(
            key,
            name,
            (int)Math.Round(Math.Clamp(raw, 0, 1) * 100),
            highIsGood,
            description);
}
