using Honey.Desktop.Settings;

namespace Honey.Desktop.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_文件不存在时返回默认设置()
    {
        var path = TemporaryPath();
        var settings = await new SettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task SaveAsync_原子保存且不留下临时文件()
    {
        var path = TemporaryPath();
        var store = new SettingsStore(path);

        await store.SaveAsync(
            new AppSettings { PetSize = 199 },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            199,
            (await store.LoadAsync(TestContext.Current.CancellationToken)).PetSize);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public async Task LoadAsync_损坏文件被保留为诊断副本()
    {
        var path = TemporaryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{broken", TestContext.Current.CancellationToken);

        var settings = await new SettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new AppSettings(), settings);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.json.corrupt-*"));
    }

    [Fact]
    public async Task SaveAsync_已取消时不会落盘()
    {
        var path = TemporaryPath();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SettingsStore(path).SaveAsync(new AppSettings(), cancellation.Token));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SaveAsync_并发保存串行且结果始终可读取()
    {
        var path = TemporaryPath();
        var store = new SettingsStore(path);
        var saves = Enumerable.Range(60, 12)
            .Select(size => store.SaveAsync(
                new AppSettings { PetSize = size },
                TestContext.Current.CancellationToken));

        await Task.WhenAll(saves);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.InRange(loaded.PetSize, 60, 71);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    private static string TemporaryPath() =>
        Path.Combine(Path.GetTempPath(), "Honey.Tests", Guid.NewGuid().ToString("N"), "settings.json");
}
