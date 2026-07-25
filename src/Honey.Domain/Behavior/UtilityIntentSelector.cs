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
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = candidate.Utility - (previous == candidate.Key ? 0.15 : 0)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        if (availableCandidates.Length == 0)
        {
            throw new InvalidOperationException("没有可用意图。");
        }

        const double closeScoreRange = 0.03;
        var randomFactor = Math.Clamp(random01, 0, 1);
        var topCandidate = availableCandidates[0];

        if (availableCandidates.Length > 1
            && topCandidate.Score - availableCandidates[1].Score <= closeScoreRange
            && randomFactor >= 0.5)
        {
            return availableCandidates[1].Candidate;
        }

        return topCandidate.Candidate;
    }
}
