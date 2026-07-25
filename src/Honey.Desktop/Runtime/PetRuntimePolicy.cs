using Honey.Domain.Model;

namespace Honey.Desktop.Runtime;

public static class PetRuntimePolicy
{
    public static PetMood ResolveMood(PetNeeds needs, PetMode mode)
    {
        if (mode == PetMode.Berserk && needs.Stress >= 0.65)
        {
            return PetMood.Angry;
        }

        if (needs.Hunger >= 0.7)
        {
            return PetMood.Hungry;
        }

        if (needs.Energy <= 0.2)
        {
            return PetMood.Sleepy;
        }

        if (needs.Stress >= 0.65)
        {
            return PetMood.Alert;
        }

        if (needs.Curiosity >= 0.65)
        {
            return PetMood.Curious;
        }

        return needs.Affection >= 0.55 ? PetMood.Happy : PetMood.Curious;
    }

    public static TimeSpan IntentInterval(string activityLevel, bool focusMode)
    {
        var interval = activityLevel switch
        {
            "quiet" => TimeSpan.FromSeconds(6),
            "active" => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(3.5)
        };
        return focusMode ? interval * 2 : interval;
    }

    public static PetMode ApplyModePreference(string preference, PetMode current) =>
        preference switch
        {
            "normal" => PetMode.Normal,
            "berserk" => PetMode.Berserk,
            _ => current
        };
}
