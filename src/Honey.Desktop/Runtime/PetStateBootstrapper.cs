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
            return await store.LoadAsync(PrimaryPetIdentity.Id, cancellationToken)
                ?? CreatePrimary(species, now);
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
}
