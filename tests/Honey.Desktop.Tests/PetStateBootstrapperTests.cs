using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Runtime;
using Honey.Domain.Model;
using Honey.Persistence;

namespace Honey.Desktop.Tests;

public sealed class PetStateBootstrapperTests
{
    [Fact]
    public async Task LoadOrCreateAsync_无记录与读取失败都使用固定主宠Id()
    {
        var emptyStore = new MemoryStore();
        var failedStore = new MemoryStore { LoadError = new InvalidDataException("损坏") };
        var pack = new WhiteJadeSpiderPack();

        var first = await PetStateBootstrapper.LoadOrCreateAsync(
            emptyStore, pack, DateTimeOffset.UtcNow, null, TestContext.Current.CancellationToken);
        var fallback = await PetStateBootstrapper.LoadOrCreateAsync(
            failedStore, pack, DateTimeOffset.UtcNow, null, TestContext.Current.CancellationToken);

        Assert.Equal(PrimaryPetIdentity.Id, first.PetId);
        Assert.Equal(PrimaryPetIdentity.Id, fallback.PetId);
    }

    [Fact]
    public async Task LoadOrCreateAsync_回退保存后下次从相同Id加载()
    {
        var store = new MemoryStore();
        var pack = new WhiteJadeSpiderPack();
        var fallback = await PetStateBootstrapper.LoadOrCreateAsync(
            store, pack, DateTimeOffset.UtcNow, null, TestContext.Current.CancellationToken);
        await store.SaveAsync(fallback, TestContext.Current.CancellationToken);

        var loaded = await PetStateBootstrapper.LoadOrCreateAsync(
            store, pack, DateTimeOffset.UtcNow.AddMinutes(1), null, TestContext.Current.CancellationToken);

        Assert.Equal(fallback, loaded);
        Assert.Equal(PrimaryPetIdentity.Id, loaded.PetId);
    }

    private sealed class MemoryStore : IPetStateStore
    {
        public PetState? State { get; private set; }
        public Exception? LoadError { get; init; }
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveAsync(PetState state, CancellationToken cancellationToken)
        {
            State = state;
            return Task.CompletedTask;
        }

        public Task<PetState?> LoadAsync(Guid petId, CancellationToken cancellationToken) =>
            LoadError is null
                ? Task.FromResult(State)
                : Task.FromException<PetState?>(LoadError);
    }
}
