using Honey.Domain.Activity;
using Honey.Domain.Model;

namespace Honey.Desktop.Status;

public sealed record PetStatusSnapshot(
    Guid PetId,
    string SpeciesId,
    string DisplayName,
    PetMood Mood,
    PetMode Mode,
    IReadOnlyList<PetNeedGauge> Needs,
    string Behavior,
    string Phase,
    BehaviorOrigin Origin,
    TimeSpan BehaviorDuration,
    IReadOnlyList<PetActivityEntry> RecentActivities,
    int? Level = null,
    double? Experience = null,
    string? RelationshipStage = null);
