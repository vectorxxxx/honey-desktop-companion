using Honey.Domain.Model;
using Honey.Domain.Species;

namespace Honey.Content.WhiteJadeSpider;

public sealed class WhiteJadeSpiderPack : ISpeciesPack
{
    private static readonly IReadOnlyList<IBehaviorDefinition> BehaviorDefinitions =
        WhiteJadeSpiderBehaviors.Create();

    public SpeciesManifest Manifest { get; } = new(
        "honey.white-jade-spider",
        new Version(1, 0),
        "白玉蜘蛛");

    public IReadOnlyList<IBehaviorDefinition> Behaviors => BehaviorDefinitions;

    public IReadOnlyList<IInteractionRule> Interactions { get; } = [];

    public IProgressionPolicy Progression { get; } = new DisabledProgressionPolicy();

    public PetState CreateInitialState(DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            Manifest.SpeciesId,
            new PetNeeds(0.25, 0.85, 0.65, 0.5, 0.1),
            PetMood.Curious,
            PetMode.Normal,
            0.75,
            0.75,
            1,
            null,
            now);
}
