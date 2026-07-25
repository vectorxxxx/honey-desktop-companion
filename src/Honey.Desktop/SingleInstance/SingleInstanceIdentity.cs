using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Honey.Desktop.SingleInstance;

public sealed record SingleInstanceIdentity(string MutexName, string PipeName)
{
    public static SingleInstanceIdentity Default { get; } =
        Create(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Honey"));

    public static SingleInstanceIdentity Create(string dataRoot) =>
        Create(dataRoot, GetCurrentUserSid());

    public static SingleInstanceIdentity Create(string dataRoot, string userSid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        var canonicalPath = PhysicalPathCanonicalizer.Resolve(dataRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var identityMaterial = $"{userSid.ToUpperInvariant()}\n{canonicalPath}";
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identityMaterial)))[..24];
        return new SingleInstanceIdentity(
            $@"Local\Honey.Desktop.Singleton.{hash}",
            $"Honey.Desktop.Commands.{hash}");
    }

    private static string GetCurrentUserSid() =>
        WindowsIdentity.GetCurrent().User?.Value
        ?? throw new InvalidOperationException("无法确定当前 Windows 用户 SID。");
}

internal static class PhysicalPathCanonicalizer
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    public static string Resolve(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("单实例物理路径解析仅支持 Windows。");
        }

        var fullPath = Path.GetFullPath(path);
        var remainder = new Stack<string>();
        var cursor = fullPath;
        while (!Directory.Exists(cursor))
        {
            if (File.Exists(cursor))
            {
                throw new IOException("数据目录指向文件，无法创建单实例身份。");
            }

            var leaf = Path.GetFileName(cursor);
            if (string.IsNullOrEmpty(leaf))
            {
                throw new DirectoryNotFoundException($"找不到可解析的数据目录祖先：{fullPath}");
            }
            remainder.Push(leaf);
            cursor = Path.GetDirectoryName(cursor)
                ?? throw new DirectoryNotFoundException($"找不到可解析的数据目录祖先：{fullPath}");
        }

        var physical = ResolveExistingDirectory(cursor);
        while (remainder.Count > 0)
        {
            physical = Path.Combine(physical, remainder.Pop());
        }
        return Path.GetFullPath(physical);
    }

    private static string ResolveExistingDirectory(string path)
    {
        using var handle = CreateFile(
            path,
            0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"无法解析数据目录的物理路径：{path}");
        }

        var capacity = 512;
        while (true)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)capacity, 0);
            if (length == 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"无法读取数据目录的物理路径：{path}");
            }
            if (length < capacity)
            {
                return NormalizeDevicePath(buffer.ToString());
            }
            capacity = checked((int)length + 1);
        }
    }

    private static string NormalizeDevicePath(string path)
    {
        const string uncPrefix = @"\\?\UNC\";
        const string devicePrefix = @"\\?\";
        if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[uncPrefix.Length..];
        }
        return path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase)
            ? path[devicePrefix.Length..]
            : path;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW",
        SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle fileHandle,
        StringBuilder filePath,
        uint filePathLength,
        uint flags);
}
