using Honey.Desktop.Settings;
using Honey.Integrations.Security;

namespace Honey.Desktop.Tests;

public sealed class AiSettingsSaveCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_普通设置保存失败时恢复原密钥()
    {
        var secrets = new MemorySecretStore { Value = "old-key" };
        var coordinator = new AiSettingsSaveCoordinator(
            secrets,
            (_, _) => throw new InvalidOperationException("设置保存失败"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.ApplyAsync(
                "old-key",
                new AiSettingsSubmission(new AppSettings(), "new-key", false),
                TestContext.Current.CancellationToken));

        Assert.Equal("old-key", secrets.Value);
    }

    private sealed class MemorySecretStore : IAiSecretStore
    {
        public string? Value { get; set; }

        public Task SaveAsync(string secret, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Value = secret;
            return Task.CompletedTask;
        }

        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Value);

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Value = null;
            return Task.CompletedTask;
        }
    }
}
