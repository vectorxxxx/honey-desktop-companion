using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Microsoft.Data.Sqlite;

namespace Honey.Persistence.Tests;

public sealed class SqlitePetStateStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsCompleteState()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        var expected = CreateState();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(expected, TestContext.Current.CancellationToken);
        var actual = await store.LoadAsync(expected.PetId, TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SaveAsync_UpsertsLatestState()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        var original = CreateState();
        var latest = original with
        {
            Mood = PetMood.Happy,
            Needs = original.Needs with { Hunger = 0.9 },
            UpdatedAt = original.UpdatedAt.AddMinutes(1)
        };

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(original, TestContext.Current.CancellationToken);
        await store.SaveAsync(latest, TestContext.Current.CancellationToken);

        Assert.Equal(latest, await store.LoadAsync(original.PetId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_WhenStateDoesNotExist_ReturnsNull()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Null(await store.LoadAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupted_ThrowsInvalidDataExceptionContainingPetId()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        var petId = Guid.NewGuid();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await InsertRawStateAsync(
            database.Path,
            petId,
            "{not-json}",
            "honey.white-jade-spider",
            new DateTimeOffset(2026, 7, 25, 12, 30, 0, TimeSpan.FromHours(8)));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => store.LoadAsync(petId, TestContext.Current.CancellationToken));

        Assert.Contains(petId.ToString(), exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task LoadAsync_WhenNeedIsInvalid_ThrowsInvalidDataException(double hunger)
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        var state = CreateState() with { Needs = CreateState().Needs with { Hunger = hunger } };
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await InsertRawStateAsync(
            database.Path,
            state.PetId,
            System.Text.Json.JsonSerializer.Serialize(state, JsonOptionsForTest),
            state.SpeciesId,
            state.UpdatedAt);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => store.LoadAsync(state.PetId, TestContext.Current.CancellationToken));

        Assert.Contains(state.PetId.ToString(), exception.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenStoredPetIdDiffersFromQuery_ThrowsInvalidDataException()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        var queryId = Guid.NewGuid();
        var otherState = CreateState();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await InsertRawStateAsync(
            database.Path,
            queryId,
            System.Text.Json.JsonSerializer.Serialize(otherState, JsonOptionsForTest),
            otherState.SpeciesId,
            otherState.UpdatedAt);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => store.LoadAsync(queryId, TestContext.Current.CancellationToken));

        Assert.Contains(queryId.ToString(), exception.Message);
    }

    [Fact]
    public async Task PublicAsyncMethods_WhenTokenAlreadyCanceled_ThrowOperationCanceledException()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.InitializeAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(CreateState(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.LoadAsync(Guid.NewGuid(), cancellation.Token));
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptionsForTest = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static PetState CreateState() => new(
        Guid.NewGuid(),
        "honey.white-jade-spider",
        new PetNeeds(0.25, 0.85, 0.65, 0.5, 0.1),
        PetMood.Curious,
        PetMode.Normal,
        123.5,
        -45.25,
        1.2,
        new BehaviorKey("play"),
        new DateTimeOffset(2026, 7, 25, 12, 30, 0, TimeSpan.FromHours(8)));

    private static async Task InsertRawStateAsync(
        string path,
        Guid key,
        string json,
        string speciesId,
        DateTimeOffset updatedAt)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO pet_state(pet_id, species_id, state_json, updated_at)
            VALUES ($petId, $speciesId, $stateJson, $updatedAt);
            """;
        command.Parameters.AddWithValue("$petId", key.ToString());
        command.Parameters.AddWithValue("$speciesId", speciesId);
        command.Parameters.AddWithValue("$stateJson", json);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}

internal sealed class TemporaryDatabase : IDisposable
{
    private readonly string _directory;

    public TemporaryDatabase()
    {
        _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"Honey.Persistence.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "honey.db");
    }

    public string Path { get; }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_directory))
        {
            File.Delete(file);
        }

        Directory.Delete(_directory);
    }
}
