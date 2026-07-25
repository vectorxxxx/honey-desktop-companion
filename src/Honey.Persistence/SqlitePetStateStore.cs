using System.Text.Json;
using System.Text.Json.Serialization;
using Honey.Domain.Model;
using Microsoft.Data.Sqlite;

namespace Honey.Persistence;

public sealed class SqlitePetStateStore : IPetStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _databasePath;
    private readonly SchemaMigrator _migrator;

    public SqlitePetStateStore(string databasePath)
        : this(databasePath, new SchemaMigrator())
    {
    }

    internal SqlitePetStateStore(string databasePath, SchemaMigrator migrator)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径不能为空。", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException($"无法确定数据库目录：{_databasePath}");
        Directory.CreateDirectory(directory);

        if (File.Exists(_databasePath) && new FileInfo(_databasePath).Length > 0)
        {
            File.Copy(_databasePath, _databasePath + ".backup", overwrite: true);
        }

        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await _migrator.MigrateAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"数据库迁移失败。路径：{_databasePath}；当前支持版本：{SchemaMigrator.CurrentVersion}。{exception.Message}",
                exception);
        }
    }

    public async Task SaveAsync(PetState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateState(state, state.PetId);

        var json = JsonSerializer.Serialize(state, JsonOptions);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO pet_state(pet_id, species_id, state_json, updated_at)
                VALUES ($petId, $speciesId, $stateJson, $updatedAt)
                ON CONFLICT(pet_id) DO UPDATE SET
                    species_id = excluded.species_id,
                    state_json = excluded.state_json,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("$petId", state.PetId.ToString());
            command.Parameters.AddWithValue("$speciesId", state.SpeciesId);
            command.Parameters.AddWithValue("$stateJson", json);
            command.Parameters.AddWithValue("$updatedAt", state.UpdatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PetState?> LoadAsync(Guid petId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (petId == Guid.Empty)
        {
            throw new ArgumentException("灵兽 ID 不能为空。", nameof(petId));
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT species_id, state_json, updated_at
            FROM pet_state
            WHERE pet_id = $petId;
            """;
        command.Parameters.AddWithValue("$petId", petId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        try
        {
            var storedSpeciesId = reader.GetString(0);
            var json = reader.GetString(1);
            var storedUpdatedAtText = reader.GetString(2);
            var state = JsonSerializer.Deserialize<PetState>(json, JsonOptions)
                ?? throw new JsonException("状态 JSON 结果为空。");

            if (!DateTimeOffset.TryParse(
                    storedUpdatedAtText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var storedUpdatedAt))
            {
                throw new InvalidDataException("数据库中的更新时间无法表示。");
            }

            ValidateState(state, petId);
            if (!string.Equals(
                    state.SpeciesId, storedSpeciesId, StringComparison.Ordinal)
                || state.UpdatedAt != storedUpdatedAt)
            {
                throw new InvalidDataException("状态 JSON 与数据库索引列不一致。");
            }

            return state;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException(
                $"灵兽 {petId} 的存档损坏或内容不合法。",
                exception);
        }
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        return new SqliteConnection(builder.ConnectionString);
    }

    private static void ValidateState(PetState state, Guid expectedPetId)
    {
        if (state.PetId == Guid.Empty || state.PetId != expectedPetId)
        {
            throw new InvalidDataException("存档中的灵兽 ID 与查询 ID 不一致。");
        }

        if (string.IsNullOrWhiteSpace(state.SpeciesId))
        {
            throw new InvalidDataException("存档中的物种 ID 不能为空。");
        }

        var needs = new[]
        {
            state.Needs.Hunger,
            state.Needs.Energy,
            state.Needs.Curiosity,
            state.Needs.Affection,
            state.Needs.Stress
        };
        if (needs.Any(value => !double.IsFinite(value) || value is < 0 or > 1))
        {
            throw new InvalidDataException("存档中的需求值必须为 [0,1] 内的有限数。");
        }

        if (!double.IsFinite(state.X) || !double.IsFinite(state.Y))
        {
            throw new InvalidDataException("存档中的坐标必须为有限数。");
        }

        if (!double.IsFinite(state.Scale) || state.Scale <= 0)
        {
            throw new InvalidDataException("存档中的缩放值必须为大于零的有限数。");
        }
    }
}
