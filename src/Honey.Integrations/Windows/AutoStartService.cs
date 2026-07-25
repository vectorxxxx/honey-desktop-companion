using Microsoft.Win32;

namespace Honey.Integrations.Windows;

public interface IRunRegistry
{
    string? Read(string name);
    void Write(string name, string value);
    void Delete(string name);
}

public sealed class WindowsRunRegistry : IRunRegistry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void Write(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException("无法打开当前用户启动项注册表。");
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void Delete(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public interface IAutoStartController
{
    void Enable(string executablePath);
    void Disable();
    bool IsEnabled(string executablePath);
}

public sealed class AutoStartService : IAutoStartController
{
    public const string ValueName = "Honey";
    private readonly IRunRegistry _registry;
    private readonly Func<string, bool> _fileExists;

    public AutoStartService(IRunRegistry? registry = null, Func<string, bool>? fileExists = null)
    {
        _registry = registry ?? new WindowsRunRegistry();
        _fileExists = fileExists ?? File.Exists;
    }

    public void Enable(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (executablePath.IndexOfAny(['\r', '\n', '"']) >= 0)
        {
            throw new ArgumentException("程序路径包含非法字符。", nameof(executablePath));
        }

        var fullPath = Path.GetFullPath(executablePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase)
            || !_fileExists(fullPath))
        {
            throw new ArgumentException("开机启动路径必须指向存在的 exe 文件。", nameof(executablePath));
        }

        _registry.Write(ValueName, $"\"{fullPath}\" --background");
    }

    public void Disable() => _registry.Delete(ValueName);

    public bool IsEnabled(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);
        return string.Equals(
            _registry.Read(ValueName),
            $"\"{fullPath}\" --background",
            StringComparison.OrdinalIgnoreCase);
    }
}
