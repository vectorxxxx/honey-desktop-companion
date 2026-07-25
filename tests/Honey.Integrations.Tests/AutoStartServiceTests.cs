using Honey.Integrations.Windows;

namespace Honey.Integrations.Tests;

public sealed class AutoStartServiceTests
{
    [Fact]
    public void Enable_写入带引号的后台启动命令()
    {
        var executable = Path.Combine(
            Path.GetTempPath(),
            "Honey.Tests",
            Guid.NewGuid().ToString("N"),
            "Honey.exe");
        var registry = new FakeRunRegistry();
        var service = new AutoStartService(registry, File.Exists);

        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllBytes(executable, []);
        try
        {
            service.Enable(executable);
            Assert.Equal($"\"{Path.GetFullPath(executable)}\" --background", registry.Value);
        }
        finally
        {
            File.Delete(executable);
            Directory.Delete(Path.GetDirectoryName(executable)!);
        }
    }

    [Fact]
    public void Disable_删除Honey启动项()
    {
        var registry = new FakeRunRegistry { Value = "\"C:\\Honey.exe\" --background" };
        new AutoStartService(registry).Disable();
        Assert.Null(registry.Value);
    }

    [Theory]
    [InlineData("Honey.dll")]
    [InlineData("Honey.exe\n--evil")]
    public void Enable_拒绝非Exe或命令注入路径(string path)
    {
        Assert.Throws<ArgumentException>(
            () => new AutoStartService(new FakeRunRegistry(), _ => true).Enable(path));
    }

    private sealed class FakeRunRegistry : IRunRegistry
    {
        public string? Value { get; set; }

        public string? Read(string name) => Value;

        public void Write(string name, string value) => Value = value;

        public void Delete(string name) => Value = null;
    }
}
