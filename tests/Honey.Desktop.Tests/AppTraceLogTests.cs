using Honey.Desktop.Diagnostics;

namespace Honey.Desktop.Tests;

public sealed class AppTraceLogTests
{
    [Fact]
    public void Create_创建可追加且可读取的故障日志()
    {
        var root = Path.Combine(Path.GetTempPath(), $"honey-log-{Guid.NewGuid():N}");
        try
        {
            using (var listener = AppTraceLog.Create(root))
            {
                listener.WriteLine("测试故障");
                listener.Flush();
            }

            Assert.Contains("测试故障", File.ReadAllText(Path.Combine(root, "honey.log")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Create_达到上限时只保留一份轮转日志()
    {
        var root = Path.Combine(Path.GetTempPath(), $"honey-log-rotate-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "honey.log"), "12345678");

            using (var listener = AppTraceLog.Create(root, maximumBytes: 8))
            {
                listener.Write("new");
                listener.Flush();
            }

            Assert.Equal("12345678", File.ReadAllText(Path.Combine(root, "honey.log.1")));
            Assert.Equal("new", File.ReadAllText(Path.Combine(root, "honey.log")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
