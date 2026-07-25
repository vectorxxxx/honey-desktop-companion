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
        if (string.IsNullOrWhiteSpace(key.Value))
        {
            throw new ArgumentException("技能键不能为空。", nameof(key));
        }

        if (minimumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDuration), "最小时长必须大于零。");
        }

        if (maximumDuration < minimumDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration), "最大时长不得小于最小时长。");
        }

        if (cooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldown), "冷却时间不得为负数。");
        }

        ArgumentNullException.ThrowIfNull(phases);
        var phaseArray = phases.ToArray();
        if (phaseArray.Length == 0
            || phaseArray.Any(phase =>
                string.IsNullOrWhiteSpace(phase.Key)
                || phase.Duration <= TimeSpan.Zero))
        {
            throw new ArgumentException("技能至少需要一个键非空、正时长阶段。", nameof(phases));
        }

        if (phaseArray.Select(phase => phase.Key).Distinct(StringComparer.Ordinal).Count()
            != phaseArray.Length)
        {
            throw new ArgumentException("同一技能的阶段键必须唯一。", nameof(phases));
        }

        long timelineTicks;
        try
        {
            timelineTicks = phaseArray.Aggregate(
                0L,
                (total, phase) => checked(total + phase.Duration.Ticks));
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("阶段总时长超出可表示范围。", nameof(phases), exception);
        }
        var timelineDuration = TimeSpan.FromTicks(timelineTicks);
        if (timelineDuration < minimumDuration || timelineDuration > maximumDuration)
        {
            throw new ArgumentException("阶段总时长必须位于技能声明区间内。", nameof(phases));
        }

        Key = key;
        MinimumDuration = minimumDuration;
        MaximumDuration = maximumDuration;
        Cooldown = cooldown;
        _phases = Array.AsReadOnly(phaseArray);
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
                (BuiltInPhaseKeys.ForageDiscover, 1.5),
                (BuiltInPhaseKeys.ForageApproach, 3),
                (BuiltInPhaseKeys.ForageCapture, 1),
                (BuiltInPhaseKeys.ForageEat, 2)),
            Skill(BuiltInBehaviorKeys.Web, 8, 18, 18,
                (BuiltInPhaseKeys.WebAnchor, 2),
                (BuiltInPhaseKeys.WebSilk, 2),
                (BuiltInPhaseKeys.WebWeave, 7),
                (BuiltInPhaseKeys.WebRest, 2)),
            Skill(BuiltInBehaviorKeys.Play, 5, 10, 8,
                (BuiltInPhaseKeys.PlayBounce, 2.5),
                (BuiltInPhaseKeys.PlayChase, 4)),
            Skill(BuiltInBehaviorKeys.Observe, 2, 8, 3,
                (BuiltInPhaseKeys.ObserveTurn, 1),
                (BuiltInPhaseKeys.ObserveTrack, 3)),
            Skill(BuiltInBehaviorKeys.Pounce, 1.2, 1.2, 45,
                (BuiltInPhaseKeys.PounceCharge, 0.4),
                (BuiltInPhaseKeys.PounceLeap, 0.45),
                (BuiltInPhaseKeys.PounceRetreat, 0.35)),
            Skill(BuiltInBehaviorKeys.Groom, 3, 6, 12,
                (BuiltInPhaseKeys.GroomStart, 1),
                (BuiltInPhaseKeys.GroomAlternate, 2),
                (BuiltInPhaseKeys.GroomFinish, 1)),
            Skill(BuiltInBehaviorKeys.Sleep, 20, 30, 20,
                (BuiltInPhaseKeys.SleepCurl, 3),
                (BuiltInPhaseKeys.SleepBreathe, 21))
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
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "推进时长不得为负数。");
        }

        var remainingTicks = skill.TimelineDuration.Ticks - _elapsed.Ticks;
        _elapsed = elapsed.Ticks >= remainingTicks
            ? skill.TimelineDuration
            : _elapsed + elapsed;
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
