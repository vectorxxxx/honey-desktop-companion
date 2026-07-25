using Microsoft.Data.Sqlite;

namespace Honey.Persistence;

public sealed record SqliteArchiveVerification(int SchemaVersion, int StateCount);

public static class SqliteArchiveVerifier
{
    private static readonly string[] RequiredTables =
    [
        "pet_state",
        "relationship_edge",
        "progression_state"
    ];

    public static async Task<SqliteArchiveVerification> VerifyAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            throw new InvalidDataException("SQLite 存档不存在。");
        }

        try
        {
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = Path.GetFullPath(databasePath),
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await RequireOkAsync(connection, "PRAGMA quick_check;", cancellationToken);
            await RequireOkAsync(connection, "PRAGMA integrity_check;", cancellationToken);

            var version = await ScalarIntAsync(
                connection,
                "PRAGMA user_version;",
                cancellationToken);
            if (version != SchemaMigrator.CurrentVersion)
            {
                throw new InvalidDataException($"SQLite 架构版本无效：{version}。");
            }

            foreach (var table in RequiredTables)
            {
                await using var schema = connection.CreateCommand();
                schema.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
                schema.Parameters.AddWithValue("$name", table);
                if (Convert.ToInt32(
                        await schema.ExecuteScalarAsync(cancellationToken),
                        System.Globalization.CultureInfo.InvariantCulture) != 1)
                {
                    throw new InvalidDataException($"SQLite 缺少表：{table}。");
                }
            }

            await using var state = connection.CreateCommand();
            state.CommandText =
                "SELECT COUNT(*), COALESCE(MIN(length(state_json)), 0) FROM pet_state;";
            await using var reader = await state.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException("SQLite 状态查询没有返回结果。");
            }
            var count = reader.GetInt32(0);
            if (count < 1 || reader.GetInt32(1) < 2)
            {
                throw new InvalidDataException("SQLite 没有可读取的灵兽状态记录。");
            }

            return new SqliteArchiveVerification(version, count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            throw new InvalidDataException("SQLite 存档无法通过完整性验证。", exception);
        }
    }

    private static async Task RequireOkAsync(
        SqliteConnection connection,
        string statement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }
        if (values.Count != 1
            || !string.Equals(values[0], "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SQLite 完整性检查失败：{string.Join("；", values)}");
        }
    }

    private static async Task<int> ScalarIntAsync(
        SqliteConnection connection,
        string statement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
