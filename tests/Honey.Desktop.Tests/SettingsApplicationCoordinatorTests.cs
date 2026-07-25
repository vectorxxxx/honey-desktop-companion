using Honey.Desktop.Settings;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Tests;

public sealed class SettingsApplicationCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_保存失败会恢复旧设置与旧启动状态()
    {
        var oldSettings = new AppSettings { StartWithWindows = false, PetSize = 140 };
        var store = new FakeSettingsPersistence(oldSettings) { FailNextSave = true };
        var autoStart = new FakeAutoStartController();
        var coordinator = new SettingsApplicationCoordinator(store, autoStart);

        await Assert.ThrowsAsync<IOException>(
            () => coordinator.ApplyAsync(
                oldSettings,
                oldSettings with { StartWithWindows = true, PetSize = 200 },
                @"C:\Honey.exe",
                TestContext.Current.CancellationToken));

        Assert.Equal(oldSettings, store.Current);
        Assert.False(autoStart.Enabled);
    }

    [Fact]
    public async Task ApplyAsync_注册表失败不会改变设置文件()
    {
        var oldSettings = new AppSettings { StartWithWindows = false };
        var store = new FakeSettingsPersistence(oldSettings);
        var autoStart = new FakeAutoStartController { FailEnable = true };
        var coordinator = new SettingsApplicationCoordinator(store, autoStart);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.ApplyAsync(
                oldSettings,
                oldSettings with { StartWithWindows = true },
                @"C:\Honey.exe",
                TestContext.Current.CancellationToken));

        Assert.Equal(oldSettings, store.Current);
        Assert.False(autoStart.Enabled);
    }

    private sealed class FakeSettingsPersistence(AppSettings current) : ISettingsPersistence
    {
        public AppSettings Current { get; private set; } = current;
        public bool FailNextSave { get; set; }
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("保存失败");
            }

            Current = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAutoStartController : IAutoStartController
    {
        public bool Enabled { get; private set; }
        public bool FailEnable { get; init; }
        public void Enable(string executablePath)
        {
            if (FailEnable)
            {
                throw new InvalidOperationException("注册表失败");
            }

            Enabled = true;
        }

        public void Disable() => Enabled = false;
        public bool IsEnabled(string executablePath) => Enabled;
    }
}
