using Microsoft.Data.Sqlite;

namespace Honey.Persistence.Tests;

public sealed class SchemaMigratorTests
{
    [Fact]
    public void AppDataPaths_ConstructorHasNoSideEffectsAndEnsureDirectoriesCreatesStablePaths()
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"Honey.Persistence.Paths-{Guid.NewGuid():N}");

        try
        {
            var paths = new AppDataPaths(root);

            Assert.False(Directory.Exists(root));
            Assert.Equal(System.IO.Path.Combine(root, "honey.db"), paths.DatabasePath);
            Assert.Equal(System.IO.Path.Combine(root, "logs"), paths.LogsDirectory);

            paths.EnsureDirectories();

            Assert.True(Directory.Exists(root));
            Assert.True(Directory.Exists(paths.LogsDirectory));
        }
        finally
        {
            if (Directory.Exists(System.IO.Path.Combine(root, "logs")))
            {
                Directory.Delete(System.IO.Path.Combine(root, "logs"));
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root);
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotentAndCreatesVersionOneSchema()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var connection =
            new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1L, await ScalarInt64Async(
            connection, "PRAGMA user_version;", TestContext.Current.CancellationToken));
        var tables = await ReadTableNamesAsync(connection, TestContext.Current.CancellationToken);
        Assert.Contains("pet_state", tables);
        Assert.Contains("relationship_edge", tables);
        Assert.Contains("progression_state", tables);
    }

    [Fact]
    public async Task InitializeAsync_WhenCommittedDataRemainsInWal_CreatesConsistentBackup()
    {
        using var database = new TemporaryDatabase();
        await using var writer =
            new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await writer.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(
            writer,
            """
            PRAGMA journal_mode = WAL;
            PRAGMA wal_autocheckpoint = 0;
            CREATE TABLE sentinel(value TEXT NOT NULL);
            INSERT INTO sentinel(value) VALUES ('WAL 中的已提交数据');
            """,
            TestContext.Current.CancellationToken);
        var store = new SqlitePetStateStore(database.Path);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(database.Path + ".backup"));
        await using var backup =
            new SqliteConnection($"Data Source={database.Path}.backup;Mode=ReadOnly;Pooling=False");
        await backup.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            "WAL 中的已提交数据",
            (string?)await ScalarAsync(
                backup, "SELECT value FROM sentinel;", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenMainDatabaseIsCorrupted_PreservesKnownGoodBackup()
    {
        using var database = new TemporaryDatabase();
        await CreateVersionZeroDatabaseAsync(database.Path, "良好备份");
        var store = new SqlitePetStateStore(database.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var expectedBackup = await File.ReadAllBytesAsync(
            database.Path + ".backup", TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(
            database.Path, "损坏的主数据库", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            expectedBackup,
            await File.ReadAllBytesAsync(
                database.Path + ".backup", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyCurrent_DoesNotOverwriteBackup()
    {
        using var database = new TemporaryDatabase();
        await CreateVersionZeroDatabaseAsync(database.Path, "迁移前快照");
        var store = new SqlitePetStateStore(database.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var expectedBackup = await File.ReadAllBytesAsync(
            database.Path + ".backup", TestContext.Current.CancellationToken);

        await ExecuteAsync(
            database.Path,
            "INSERT INTO sentinel(value) VALUES ('迁移后的新数据');",
            TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            expectedBackup,
            await File.ReadAllBytesAsync(
                database.Path + ".backup", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenDatabaseHasFutureVersion_PreservesOriginalAndBackup()
    {
        using var database = new TemporaryDatabase();
        await using (var connection =
            new SqliteConnection($"Data Source={database.Path};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var setupCommand = connection.CreateCommand();
            setupCommand.CommandText =
                """
                CREATE TABLE sentinel(value TEXT NOT NULL);
                INSERT INTO sentinel(value) VALUES ('保留');
                PRAGMA user_version = 99;
                """;
            await setupCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await CreateVersionZeroDatabaseAsync(database.Path + ".backup", "既有良好备份");
        var expectedBackup = await File.ReadAllBytesAsync(
            database.Path + ".backup", TestContext.Current.CancellationToken);
        var store = new SqlitePetStateStore(database.Path);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("99", exception.Message);
        Assert.Contains("1", exception.Message);
        Assert.Contains(database.Path, exception.Message);
        Assert.True(File.Exists(database.Path + ".backup"));
        Assert.Equal(
            expectedBackup,
            await File.ReadAllBytesAsync(
                database.Path + ".backup", TestContext.Current.CancellationToken));

        await using var verification =
            new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await verification.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(99L, await ScalarInt64Async(
            verification, "PRAGMA user_version;", TestContext.Current.CancellationToken));
        await using var command = verification.CreateCommand();
        command.CommandText = "SELECT value FROM sentinel;";
        Assert.Equal(
            "保留",
            (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenDatabaseIsNew_DoesNotCreateBackup()
    {
        using var database = new TemporaryDatabase();
        var store = new SqlitePetStateStore(database.Path);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(database.Path + ".backup"));
    }

    [Fact]
    public async Task InitializeAsync_FromMultipleStoresOnSamePath_IsSerializedAndIdempotent()
    {
        using var database = new TemporaryDatabase();
        var stores = Enumerable.Range(0, 8)
            .Select(_ => new SqlitePetStateStore(database.Path))
            .ToArray();

        await Task.WhenAll(stores.Select(
            store => store.InitializeAsync(TestContext.Current.CancellationToken)));

        await using var connection =
            new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1L, await ScalarInt64Async(
            connection, "PRAGMA user_version;", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenSecondDdlConflicts_RollsBackFirstDdlAndVersion()
    {
        using var database = new TemporaryDatabase();
        await using (var connection =
            new SqliteConnection($"Data Source={database.Path};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(
                connection,
                "CREATE TABLE relationship_edge(conflict TEXT NOT NULL);",
                TestContext.Current.CancellationToken);
        }

        var store = new SqlitePetStateStore(database.Path);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.InitializeAsync(TestContext.Current.CancellationToken));

        await using var verification =
            new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await verification.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0L, await ScalarInt64Async(
            verification, "PRAGMA user_version;", TestContext.Current.CancellationToken));
        Assert.Equal(
            0L,
            await ScalarInt64Async(
                verification,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='pet_state';",
                TestContext.Current.CancellationToken));
    }

    private static async Task<long> ScalarInt64Async(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException());
    }

    private static async Task<object?> ScalarAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        string path,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection =
            new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, sql, cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateVersionZeroDatabaseAsync(string path, string value)
    {
        await using var connection =
            new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE sentinel(value TEXT NOT NULL);
            INSERT INTO sentinel(value) VALUES ($value);
            """;
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
