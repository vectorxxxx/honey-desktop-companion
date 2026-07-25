using Honey.Domain.Model;

namespace Honey.Persistence;

public interface IPetStateStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task SaveAsync(PetState state, CancellationToken cancellationToken);

    Task<PetState?> LoadAsync(Guid petId, CancellationToken cancellationToken);
}
