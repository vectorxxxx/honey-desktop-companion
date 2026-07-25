using Honey.Domain.Behavior;

namespace Honey.Content.WhiteJadeSpider;

public sealed record SkillPhase(string Key, TimeSpan Duration);

public sealed class SkillDefinition
{
    private readonly IReadOnlyList<SkillPhase> _phases;

    public SkillDefinition(
        BehaviorKey key,
        TimeSpan minimumDuration,
        TimeSpan maximumDuration,
        TimeSpan cooldown,
        IEnumerable<SkillPhase> phases)
    {
        Key = key;
        MinimumDuration = minimumDuration;
        MaximumDuration = maximumDuration;
        Cooldown = cooldown;
        _phases = Array.AsReadOnly(phases.ToArray());
        if (_phases.Count == 0 || _phases.Any(phase => phase.Duration <= TimeSpan.Zero))
        {
            throw new ArgumentException("技能至少需要一个正时长阶段。", nameof(phases));
        }
    }

    public BehaviorKey Key { get; }
    public TimeSpan MinimumDuration { get; }
    public TimeSpan MaximumDuration { get; }
    public TimeSpan Cooldown { get; }
    public IReadOnlyList<SkillPhase> Phases => _phases;
    public TimeSpan TimelineDuration => TimeSpan.FromTicks(_phases.Sum(phase => phase.Duration.Ticks));

    public bool CanStart(TimeSpan sinceLast)
    {
        if (sinceLast == Timeout.InfiniteTimeSpan)
        {
            return true;
        }

        return sinceLast >= TimeSpan.Zero && (sinceLast == TimeSpan.Zero || sinceLast >= Cooldown);
    }
}

public static class WhiteJadeSpiderSkills
{
    public static IReadOnlyList<SkillDefinition> All { get; } =
        Array.AsReadOnly(
        [
            Skill(BuiltInBehaviorKeys.Forage, 6, 12, 10,
                ("发现灵蝶", 1.5), ("追近", 3), ("扑获", 1), ("进食", 2)),
            Skill(BuiltInBehaviorKeys.Web, 8, 18, 18,
                ("选锚", 2), ("吐丝", 2), ("往返织网", 7), ("挂网休息", 2)),
            Skill(BuiltInBehaviorKeys.Play, 5, 10, 8,
                ("丝球弹跳", 2.5), ("追逐", 4)),
            Skill(BuiltInBehaviorKeys.Observe, 2, 8, 3,
                ("转向", 1), ("主眼跟随", 3)),
            Skill(BuiltInBehaviorKeys.Pounce, 1.2, 1.2, 45,
                ("蓄力", 0.4), ("短跳", 0.45), ("回退", 0.35)),
            Skill(BuiltInBehaviorKeys.Groom, 3, 6, 12,
                ("前足梳理", 4)),
            Skill(BuiltInBehaviorKeys.Sleep, 20, 30, 20,
                ("蜷缩", 3), ("呼吸光", 21))
        ]);

    private static SkillDefinition Skill(
        string key,
        double minimum,
        double maximum,
        double cooldown,
        params (string Key, double Seconds)[] phases) =>
        new(
            new BehaviorKey(key),
            TimeSpan.FromSeconds(minimum),
            TimeSpan.FromSeconds(maximum),
            TimeSpan.FromSeconds(cooldown),
            phases.Select(phase => new SkillPhase(phase.Key, TimeSpan.FromSeconds(phase.Seconds))));
}

public readonly record struct SkillFrame(
    BehaviorKey Key,
    string Phase,
    double Progress,
    double TotalProgress,
    bool Completed);

public sealed class SkillPlayer
{
    private SkillDefinition? _skill;
    private TimeSpan _elapsed;

    public bool IsPlaying => _skill is not null;

    public void Start(SkillDefinition skill)
    {
        _skill = skill ?? throw new ArgumentNullException(nameof(skill));
        _elapsed = TimeSpan.Zero;
    }

    public SkillFrame Advance(TimeSpan elapsed)
    {
        var skill = _skill ?? throw new InvalidOperationException("当前没有正在播放的技能。");
        if (elapsed > TimeSpan.Zero)
        {
            _elapsed += elapsed;
        }

        var completed = _elapsed >= skill.TimelineDuration;
        var bounded = completed ? skill.TimelineDuration : _elapsed;
        var phaseStart = TimeSpan.Zero;
        foreach (var phase in skill.Phases)
        {
            var phaseEnd = phaseStart + phase.Duration;
            if (bounded < phaseEnd || phase == skill.Phases[^1])
            {
                var phaseElapsed = bounded - phaseStart;
                var frame = new SkillFrame(
                    skill.Key,
                    phase.Key,
                    Math.Clamp(phaseElapsed.TotalSeconds / phase.Duration.TotalSeconds, 0, 1),
                    Math.Clamp(bounded.TotalSeconds / skill.TimelineDuration.TotalSeconds, 0, 1),
                    completed);
                if (completed)
                {
                    _skill = null;
                }

                return frame;
            }

            phaseStart = phaseEnd;
        }

        throw new InvalidOperationException("技能时间线无有效阶段。");
    }
}
