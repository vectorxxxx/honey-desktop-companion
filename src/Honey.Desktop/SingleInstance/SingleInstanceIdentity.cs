using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace Honey.Desktop.SingleInstance;

public sealed record SingleInstanceIdentity(string MutexName, string PipeName)
{
    public static SingleInstanceIdentity Default { get; } =
        Create(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Honey"));

    public static SingleInstanceIdentity Create(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var canonicalPath = Path.GetFullPath(dataRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath)))[..24];
        return new SingleInstanceIdentity(
            $@"Local\Honey.Desktop.Singleton.{hash}",
            $"Honey.Desktop.Commands.{hash}");
    }
}
