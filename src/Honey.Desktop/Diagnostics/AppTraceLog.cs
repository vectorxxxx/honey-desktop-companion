using System.Diagnostics;
using System.IO;

namespace Honey.Desktop.Diagnostics;

public static class AppTraceLog
{
    public static TextWriterTraceListener Create(string logsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        Directory.CreateDirectory(logsDirectory);
        var stream = new FileStream(
            Path.Combine(logsDirectory, "honey.log"),
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            4096,
            FileOptions.WriteThrough);
        return new TextWriterTraceListener(stream, "HoneyFile");
    }
}
