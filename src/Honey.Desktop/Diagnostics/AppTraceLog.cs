using System.Diagnostics;
using System.IO;

namespace Honey.Desktop.Diagnostics;

public static class AppTraceLog
{
    private const long DefaultMaximumBytes = 5 * 1024 * 1024;

    public static TextWriterTraceListener Create(
        string logsDirectory,
        long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        if (maximumBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }
        Directory.CreateDirectory(logsDirectory);
        var logPath = Path.Combine(logsDirectory, "honey.log");
        var rotatedPath = Path.Combine(logsDirectory, "honey.log.1");
        if (File.Exists(logPath) && new FileInfo(logPath).Length >= maximumBytes)
        {
            File.Move(logPath, rotatedPath, overwrite: true);
        }
        var stream = new FileStream(
            logPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            4096,
            FileOptions.WriteThrough);
        return new TextWriterTraceListener(stream, "HoneyFile");
    }
}
