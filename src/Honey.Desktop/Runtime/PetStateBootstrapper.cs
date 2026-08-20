using Honey.Domain.Model;
using Honey.Domain.Species;
using Honey.Persistence;

namespace Honey.Desktop.Runtime;

public static class PrimaryPetIdentity
{
    public static Guid Id { get; } =
        Guid.Parse("9f0e9f5a-7056-4ba4-92dc-706ef1401186");
}

public static class PetStateBootstrapper
{
    private static readonly DateTimeOffset LegacyNeedsSaturationCutoff =
        new(2026, 7, 29, 4, 0, 0, TimeSpan.Zero);

    public static async Task<PetState> LoadOrCreateAsync(
        IPetStateStore store,
        ISpeciesPack species,
        DateTimeOffset now,
        Action<Exception>? errorSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(species);
        try
        {
            await store.InitializeAsync(cancellationToken);
            var loaded = await store.LoadAsync(
                PrimaryPetIdentity.Id,
                cancellationToken);
            return loaded is null
                ? CreatePrimary(species, now)
                : RecoverLegacySaturatedNeeds(loaded, species, now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            try
            {
                errorSink?.Invoke(exception);
            }
            catch
            {
                // 诊断入口失败不能阻止使用固定主宠安全回退。
            }

            return CreatePrimary(species, now);
        }
    }

    private static PetState CreatePrimary(ISpeciesPack species, DateTimeOffset now) =>
        species.CreateInitialState(now) with { PetId = PrimaryPetIdentity.Id };

    private static PetState RecoverLegacySaturatedNeeds(
        PetState state,
        ISpeciesPack species,
        DateTimeOffset now)
    {
        var needs = state.Needs.Clamp();
        var isLegacySaturated =
            state.UpdatedAt < LegacyNeedsSaturationCutoff
            && needs.Hunger >= 0.999
            && needs.Energy <= 0.001
            && needs.Curiosity >= 0.999
            && needs.Stress <= 0.001;
        if (!isLegacySaturated)
        {
            return state;
        }

        var baseline = species.CreateInitialState(now).Needs.Clamp();
        return state with
        {
            Needs = baseline with { Affection = needs.Affection },
            UpdatedAt = now
        };
    }
}
