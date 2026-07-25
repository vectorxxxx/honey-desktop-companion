using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Honey.Desktop.Settings;

public interface ISettingsPersistence
{
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public sealed class SettingsStore : ISettingsPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<string, Stream> _openRead;
    private readonly Action<string, string> _moveCorrupt;
    private readonly Action<Exception>? _errorSink;

    public SettingsStore(
        string? path = null,
        Func<string, Stream>? openRead = null,
        Action<string, string>? moveCorrupt = null,
        Action<Exception>? errorSink = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Honey",
            "settings.json");
        _openRead = openRead ?? (selectedPath => new FileStream(
            selectedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan));
        _moveCorrupt = moveCorrupt ?? ((source, destination) => File.Move(source, destination));
        _errorSink = errorSink;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            try
            {
                using var stream = _openRead(_path);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                    stream,
                    JsonOptions,
                    cancellationToken).ConfigureAwait(false);
                return (settings ?? new AppSettings()).Normalize();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                PreserveCorruptFile(exception);
                return new AppSettings();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"读取设置失败，原文件保持不变：{_path}",
                    exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("设置路径缺少目录。");
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(
                directory,
                $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        settings.Normalize(),
                        JsonOptions,
                        cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void PreserveCorruptFile(JsonException parseException)
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var diagnosticPath =
            $"{_path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        try
        {
            _moveCorrupt(_path, diagnosticPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError(
                "设置内容损坏，但保留诊断副本失败。解析错误：{0}；移动错误：{1}",
                parseException,
                exception);
            try
            {
                _errorSink?.Invoke(exception);
            }
            catch (Exception sinkException)
            {
                Trace.TraceError("设置错误观察者失败：{0}", sinkException);
            }
        }
    }
}
