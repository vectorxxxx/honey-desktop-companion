using Microsoft.Data.Sqlite;

namespace Honey.Persistence;

public sealed class SchemaMigrator
{
    public const int CurrentVersion = 1;

    public async Task MigrateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException("执行迁移前必须先打开 SQLite 连接。");
        }

        var existingVersion = await ReadVersionAsync(connection, cancellationToken);
        if (existingVersion > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"数据库版本 {existingVersion} 高于当前支持版本 {CurrentVersion}，无法迁移。");
        }

        if (existingVersion == CurrentVersion)
        {
            return;
        }

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                CREATE TABLE pet_state(
                    pet_id TEXT PRIMARY KEY,
                    species_id TEXT NOT NULL,
                    state_json TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE relationship_edge(
                    source_id TEXT NOT NULL,
                    target_id TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    value REAL NOT NULL,
                    PRIMARY KEY(source_id, target_id, kind)
                );

                CREATE TABLE progression_state(
                    pet_id TEXT PRIMARY KEY,
                    experience INTEGER NOT NULL DEFAULT 0,
                    level INTEGER NOT NULL DEFAULT 1
                );

                PRAGMA user_version = 1;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<int> ReadVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
