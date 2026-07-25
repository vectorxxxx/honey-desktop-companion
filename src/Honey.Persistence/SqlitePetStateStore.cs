using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honey.Domain.Model;
using Microsoft.Data.Sqlite;

namespace Honey.Persistence;

public sealed class SqlitePetStateStore : IPetStateStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathGates =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly string _databasePath;
    private readonly SemaphoreSlim _pathGate;
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
        _pathGate = PathGates.GetOrAdd(_databasePath, _ => new SemaphoreSlim(1, 1));
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _pathGate.WaitAsync(cancellationToken);
        try
        {
            await InitializeCoreAsync(cancellationToken);
        }
        finally
        {
            _pathGate.Release();
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException($"无法确定数据库目录：{_databasePath}");
        Directory.CreateDirectory(directory);
        var existedAndWasNonEmpty =
            File.Exists(_databasePath) && new FileInfo(_databasePath).Length > 0;

        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await EnsureHealthyAsync(connection, cancellationToken);
            var version = await SchemaMigrator.GetVersionAsync(connection, cancellationToken);
            if (version > SchemaMigrator.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"数据库版本 {version} 高于当前支持版本 {SchemaMigrator.CurrentVersion}，无法迁移。");
            }

            if (existedAndWasNonEmpty && version < SchemaMigrator.CurrentVersion)
            {
                await CreateConsistentBackupAsync(connection, cancellationToken);
            }

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
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        ValidateStateForSave(state);

        await _pathGate.WaitAsync(cancellationToken);
        try
        {
            await InitializeCoreAsync(cancellationToken);
            await SaveCoreAsync(state, cancellationToken);
        }
        finally
        {
            _pathGate.Release();
        }
    }

    private async Task SaveCoreAsync(PetState state, CancellationToken cancellationToken)
    {
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
        catch (Exception originalException)
        {
            await TryRollbackWithoutMaskingAsync(transaction, originalException);
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

        await _pathGate.WaitAsync(cancellationToken);
        try
        {
            await InitializeCoreAsync(cancellationToken);
            return await LoadCoreAsync(petId, cancellationToken);
        }
        finally
        {
            _pathGate.Release();
        }
    }

    private async Task<PetState?> LoadCoreAsync(
        Guid petId,
        CancellationToken cancellationToken)
    {
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

        string? storedSpeciesId = null;
        string? json = null;
        string? storedUpdatedAtText = null;
        try
        {
            storedSpeciesId = reader.GetString(0);
            json = reader.GetString(1);
            storedUpdatedAtText = reader.GetString(2);
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
            if (json is not null)
            {
                await TryPreserveCorruptRecordAsync(
                    petId,
                    storedSpeciesId,
                    json,
                    storedUpdatedAtText,
                    exception);
            }

            throw new InvalidDataException(
                $"灵兽 {petId} 的存档损坏或内容不合法。",
                exception);
        }
    }

    private async Task TryPreserveCorruptRecordAsync(
        Guid petId,
        string? speciesId,
        string stateJson,
        string? updatedAt,
        Exception originalException)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
        var backupPath =
            $"{_databasePath}.pet-{petId:N}.corrupt-{timestamp}-{Guid.NewGuid():N}.json";
        var temporaryPath = backupPath + ".tmp";
        var record = JsonSerializer.Serialize(
            new
            {
                petId,
                speciesId,
                stateJson,
                updatedAt,
                preservedAt = DateTimeOffset.UtcNow
            },
            JsonOptions);

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                record,
                CancellationToken.None);
            File.Move(temporaryPath, backupPath);
        }
        catch (Exception backupException)
            when (backupException is IOException or UnauthorizedAccessException)
        {
            originalException.Data["Honey.Persistence.CorruptBackupException"] =
                backupException;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupException)
                when (cleanupException is IOException or UnauthorizedAccessException)
            {
                originalException.Data["Honey.Persistence.CorruptBackupCleanupException"] =
                    cleanupException;
            }
        }
    }

    private SqliteConnection CreateConnection() => CreateConnection(_databasePath);

    private async Task CreateConsistentBackupAsync(
        SqliteConnection source,
        CancellationToken cancellationToken)
    {
        var backupPath = _databasePath + ".backup";
        var temporaryPath = backupPath + ".tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var destination = CreateConnection(temporaryPath))
            {
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await using (var verification = CreateConnection(
                temporaryPath,
                SqliteOpenMode.ReadOnly))
            {
                await verification.OpenAsync(cancellationToken);
                await EnsureHealthyAsync(verification, cancellationToken);
            }

            if (File.Exists(backupPath))
            {
                File.Replace(temporaryPath, backupPath, null);
            }
            else
            {
                File.Move(temporaryPath, backupPath);
            }
        }
        finally
        {
            foreach (var temporaryFile in new[]
            {
                temporaryPath,
                temporaryPath + "-wal",
                temporaryPath + "-shm",
                temporaryPath + "-journal"
            })
            {
                if (File.Exists(temporaryFile))
                {
                    File.Delete(temporaryFile);
                }
            }
        }
    }

    private static async Task EnsureHealthyAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetString(0));
        }

        if (results.Count != 1
            || !string.Equals(results[0], "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SQLite 健康检查失败：{string.Join("；", results)}");
        }
    }

    private SqliteConnection CreateConnection(
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = mode,
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

        if (!Enum.IsDefined(state.Mood) || !Enum.IsDefined(state.Mode))
        {
            throw new InvalidDataException("存档中的情绪或模式值无效。");
        }

        if (state.PreviousBehavior is { } behavior
            && string.IsNullOrWhiteSpace(behavior.Value))
        {
            throw new InvalidDataException("存档中的上一行为键不能为空。");
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

    private static void ValidateStateForSave(PetState state)
    {
        try
        {
            ValidateState(state, state.PetId);
        }
        catch (InvalidDataException exception)
        {
            throw new ArgumentException(
                $"待保存的灵兽状态不合法：{exception.Message}",
                nameof(state),
                exception);
        }
    }

    private static async Task TryRollbackWithoutMaskingAsync(
        SqliteTransaction transaction,
        Exception originalException)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            originalException.Data["Honey.Persistence.RollbackException"] = rollbackException;
        }
    }
}
