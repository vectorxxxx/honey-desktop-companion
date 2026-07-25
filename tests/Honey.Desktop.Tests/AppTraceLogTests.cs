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

    [Fact]
    public async Task 长进程并发错误风暴期间活动与轮转日志始终有界()
    {
        var root = Path.Combine(Path.GetTempPath(), $"honey-log-storm-{Guid.NewGuid():N}");
        const int maximumBytes = 1024;
        try
        {
            using var listener = AppTraceLog.Create(root, maximumBytes);
            var writers = Enumerable.Range(0, 12).Select(worker => Task.Run(() =>
            {
                for (var index = 0; index < 200; index++)
                {
                    listener.WriteLine($"{worker:D2}:{index:D3}:{new string('故', 40)}");
                }
            }, TestContext.Current.CancellationToken));
            await Task.WhenAll(writers);
            listener.Flush();

            Assert.InRange(new FileInfo(Path.Combine(root, "honey.log")).Length, 1, maximumBytes);
            Assert.InRange(new FileInfo(Path.Combine(root, "honey.log.1")).Length, 1, maximumBytes);
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
