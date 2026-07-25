namespace Honey.Persistence;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        var selectedRoot = rootDirectory;
        if (selectedRoot is null)
        {
            selectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Honey");
        }

        if (string.IsNullOrWhiteSpace(selectedRoot))
        {
            throw new ArgumentException("应用数据根目录不能为空。", nameof(rootDirectory));
        }

        RootDirectory = Path.GetFullPath(selectedRoot);
        DatabasePath = Path.Combine(RootDirectory, "honey.db");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
    }

    public string RootDirectory { get; }

    public string DatabasePath { get; }

    public string LogsDirectory { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
