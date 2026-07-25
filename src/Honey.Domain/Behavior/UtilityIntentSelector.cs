namespace Honey.Domain.Behavior;

public sealed class UtilityIntentSelector
{
    public IntentCandidate Select(
        IReadOnlyCollection<IntentCandidate> candidates,
        BehaviorKey? previous,
        double random01)
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException("至少需要一个候选意图。", nameof(candidates));
        }

        var availableCandidates = candidates
            .Where(candidate => candidate.CooldownRemaining <= TimeSpan.Zero)
            .Select(candidate => candidate with
            {
                Utility = candidate.Utility
                    - (previous == candidate.Key ? 0.15 : 0)
                    + Math.Clamp(random01, 0, 1) * 0.03
            })
            .OrderByDescending(candidate => candidate.Utility)
            .ToArray();

        if (availableCandidates.Length == 0)
        {
            throw new InvalidOperationException("没有可用意图。");
        }

        return availableCandidates[0];
    }
}
