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
}
