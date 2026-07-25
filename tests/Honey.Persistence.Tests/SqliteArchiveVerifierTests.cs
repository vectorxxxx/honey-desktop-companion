using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace Honey.Persistence.Tests;

public sealed class SqliteArchiveVerifierTests
{
    [Fact]
    public async Task VerifyAsync_完整架构和状态记录通过()
    {
        var path = NewPath();
        try
        {
            var store = new SqlitePetStateStore(path);
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await InsertStateAsync(path);
            var before = SnapshotDirectory(path);

            var result = await SqliteArchiveVerifier.VerifyAsync(
                path,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.StateCount);
            Assert.Equal(SchemaMigrator.CurrentVersion, result.SchemaVersion);
            Assert.Equal(before, SnapshotDirectory(path));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_损坏文件失败且不会重建()
    {
        var path = NewPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                "not-a-database",
                TestContext.Current.CancellationToken);
            var before = SnapshotDirectory(path);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => SqliteArchiveVerifier.VerifyAsync(
                    path,
                    TestContext.Current.CancellationToken));
            Assert.Equal(
                "not-a-database",
                await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal(before, SnapshotDirectory(path));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_Wal存档通过且原目录严格不变()
    {
        var path = NewPath();
        try
        {
            await new SqlitePetStateStore(path).InitializeAsync(
                TestContext.Current.CancellationToken);
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                }.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using (var wal = connection.CreateCommand())
            {
                wal.CommandText = "PRAGMA journal_mode=WAL;";
                await wal.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            }
            await using (var insert = connection.CreateCommand())
            {
                insert.CommandText =
                    """
                    INSERT INTO pet_state(pet_id, species_id, state_json, updated_at)
                    VALUES('00000000-0000-0000-0000-000000000002', 'test.wal', '{}', '2026-07-26T00:00:00Z');
                    """;
                await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            Assert.True(File.Exists(path + "-wal"));
            var before = SnapshotDirectory(path);

            var result = await SqliteArchiveVerifier.VerifyAsync(
                path,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.StateCount);
            Assert.Equal(before, SnapshotDirectory(path));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_没有状态记录时失败()
    {
        var path = NewPath();
        try
        {
            await new SqlitePetStateStore(path).InitializeAsync(
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => SqliteArchiveVerifier.VerifyAsync(
                    path,
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    private static async Task InsertStateAsync(string path)
    {
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO pet_state(pet_id, species_id, state_json, updated_at)
            VALUES('00000000-0000-0000-0000-000000000001', 'test.species', '{}', '2026-07-26T00:00:00Z');
            """;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string NewPath() => Path.Combine(
        Path.GetTempPath(),
        $"honey-verifier-{Guid.NewGuid():N}",
        "honey.db");

    private static void DeleteDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IReadOnlyList<string> SnapshotDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        return Directory.GetFiles(directory)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
            {
                var info = new FileInfo(file);
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var hash = Convert.ToHexString(SHA256.HashData(stream));
                return $"{info.Name}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{hash}";
            })
            .ToArray();
    }
}
