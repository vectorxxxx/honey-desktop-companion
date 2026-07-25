using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honey.Integrations.Security;

public interface IAiSecretStore
{
    Task SaveAsync(string secret, CancellationToken cancellationToken = default);
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(CancellationToken cancellationToken = default);
}

public sealed class DpapiSecretStore : IAiSecretStore
{
    private const int CurrentVersion = 1;
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("Honey.Ai.Secret.v1");
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiSecretStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Honey",
            "secrets.json");
    }

    public static string Protect(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("秘密不能为空。", nameof(secret));
        }

        var plaintext = Encoding.UTF8.GetBytes(secret);
        try
        {
            return Convert.ToBase64String(
                ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("无法使用当前 Windows 用户保护秘密。", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static string Unprotect(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new ArgumentException("密文不能为空。", nameof(cipherText));
        }

        byte[] protectedBytes;
        try
        {
            protectedBytes = Convert.FromBase64String(cipherText);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("密文不是有效的 Base64。", exception);
        }

        try
        {
            var plaintext = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            try
            {
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("密文无法由当前 Windows 用户解密。", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public async Task SaveAsync(string secret, CancellationToken cancellationToken = default)
    {
        var cipherText = Protect(secret);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("秘密路径缺少目录。");
            Directory.CreateDirectory(directory);
            var temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        new SecretFile(CurrentVersion, cipherText),
                        cancellationToken: cancellationToken);
                    await stream.FlushAsync(cancellationToken);
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

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(_path))
            {
                return null;
            }

            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var file = await JsonSerializer.DeserializeAsync<SecretFile>(
                stream,
                cancellationToken: cancellationToken);
            if (file is null || file.Version != CurrentVersion)
            {
                throw new InvalidDataException("秘密文件版本无效。");
            }

            return Unprotect(file.CipherText);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record SecretFile(int Version, string CipherText);
}
