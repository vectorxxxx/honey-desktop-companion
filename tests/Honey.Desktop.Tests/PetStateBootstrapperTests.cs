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

    [Fact]
    public async Task LoadOrCreateAsync_旧版饱和需求会恢复基础值并保留亲密度()
    {
        var store = new MemoryStore();
        var pack = new WhiteJadeSpiderPack();
        var legacy = pack.CreateInitialState(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero)) with
        {
            PetId = PrimaryPetIdentity.Id,
            Needs = new PetNeeds(1, 0, 1, 0.65, 0)
        };
        await store.SaveAsync(legacy, TestContext.Current.CancellationToken);

        var loaded = await PetStateBootstrapper.LoadOrCreateAsync(
            store,
            pack,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(0.25, loaded.Needs.Hunger);
        Assert.Equal(0.85, loaded.Needs.Energy);
        Assert.Equal(0.65, loaded.Needs.Curiosity);
        Assert.Equal(0.65, loaded.Needs.Affection);
        Assert.Equal(0.1, loaded.Needs.Stress);
    }

    [Fact]
    public async Task LoadOrCreateAsync_正常存档不会被基础值覆盖()
    {
        var store = new MemoryStore();
        var pack = new WhiteJadeSpiderPack();
        var persisted = pack.CreateInitialState(DateTimeOffset.UtcNow) with
        {
            PetId = PrimaryPetIdentity.Id,
            Needs = new PetNeeds(0.7, 0.3, 0.8, 0.9, 0)
        };
        await store.SaveAsync(persisted, TestContext.Current.CancellationToken);

        var loaded = await PetStateBootstrapper.LoadOrCreateAsync(
            store,
            pack,
            DateTimeOffset.UtcNow,
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(persisted, loaded);
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
