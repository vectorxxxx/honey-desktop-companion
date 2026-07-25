using Honey.Domain.Behavior;

namespace Honey.Domain.Model;

public sealed record PetState(
    Guid PetId,
    string SpeciesId,
    PetNeeds Needs,
    PetMood Mood,
    PetMode Mode,
    double X,
    double Y,
    double Scale,
    BehaviorKey? PreviousBehavior,
    DateTimeOffset UpdatedAt)
{
    public static PetState CreateWhiteJadeSpider(DateTimeOffset now) => new(
        Guid.NewGuid(),
        "honey.white-jade-spider",
        new PetNeeds(0.25, 0.85, 0.65, 0.5, 0.1),
        PetMood.Curious,
        PetMode.Normal,
        0.75,
        0.75,
        1.0,
        null,
        now);
}
