using Honey.Domain.Behavior;

namespace Honey.Domain.Movement;

public static class PetLocomotionPolicy
{
    private const double RetreatDistance = 90;
    private const double RetreatPointerSpeed = 480;
    private const double ApproachDistance = 260;
    private const double RequiredInterestSeconds = 1;

    public static LocomotionIntent Resolve(PetLocomotionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsAnchored(context.Behavior, context.Phase))
        {
            return LocomotionIntent.Anchor;
        }

        var pointer = context.Pointer;
        if (pointer.Distance <= RetreatDistance && pointer.Speed >= RetreatPointerSpeed)
        {
            return LocomotionIntent.RetreatPointer;
        }
        if (pointer.Distance <= ApproachDistance
            && pointer.InterestSeconds >= RequiredInterestSeconds
            && pointer.ChaseCooldownRemaining <= TimeSpan.Zero)
        {
            return LocomotionIntent.ApproachPointer;
        }
        if (NeedsBehaviorTarget(context.Behavior, context.Phase))
        {
            return LocomotionIntent.BehaviorTarget;
        }
        if (string.Equals(
                context.Behavior,
                BuiltInBehaviorKeys.Observe,
                StringComparison.Ordinal))
        {
            return LocomotionIntent.Idle;
        }

        return LocomotionIntent.Roam;
    }

    private static bool IsAnchored(string behavior, string phase) =>
        string.Equals(behavior, BuiltInBehaviorKeys.Sleep, StringComparison.Ordinal)
        || string.Equals(behavior, BuiltInBehaviorKeys.Groom, StringComparison.Ordinal)
        || string.Equals(behavior, BuiltInBehaviorKeys.Web, StringComparison.Ordinal)
            && string.Equals(phase, BuiltInPhaseKeys.WebRest, StringComparison.Ordinal);

    private static bool NeedsBehaviorTarget(string behavior, string phase) =>
        string.Equals(behavior, BuiltInBehaviorKeys.Forage, StringComparison.Ordinal)
            && string.Equals(phase, BuiltInPhaseKeys.ForageApproach, StringComparison.Ordinal)
        || string.Equals(behavior, BuiltInBehaviorKeys.Play, StringComparison.Ordinal)
            && string.Equals(phase, BuiltInPhaseKeys.PlayChase, StringComparison.Ordinal)
        || string.Equals(behavior, BuiltInBehaviorKeys.Pounce, StringComparison.Ordinal)
            && string.Equals(phase, BuiltInPhaseKeys.PounceLeap, StringComparison.Ordinal);
}
