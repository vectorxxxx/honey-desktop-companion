using Honey.Desktop.Settings;
using Honey.Integrations.Security;

namespace Honey.Desktop.Tests;

public sealed class AiSettingsSaveCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_普通设置保存失败时恢复原密钥()
    {
        var config = Honey.Integrations.Ai.AiEndpointValidator.StrictValidate(
            "https://api.openai.com/v1",
            "gpt-5.6-luna");
        var old = new BoundAiSecret(
            "old-key", "old-binding", 1, config.CanonicalEndpoint, config.Model);
        var secrets = new MemorySecretStore { BoundValue = old };
        var coordinator = new AiSettingsSaveCoordinator(
            secrets,
            (_, _) => throw new InvalidOperationException("设置保存失败"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.ApplyAsync(
                old,
                new AiSettingsSubmission(new AppSettings(), "new-key", false),
                TestContext.Current.CancellationToken));

        Assert.Equal(old, secrets.BoundValue);
    }

    [Fact]
    public async Task ApplyAsync_端点变化时即使密钥未改也生成新绑定()
    {
        var oldConfig = Honey.Integrations.Ai.AiEndpointValidator.StrictValidate(
            "https://old.example/v1",
            "model");
        var old = new BoundAiSecret(
            "old-key", "old-binding", 1, oldConfig.CanonicalEndpoint, oldConfig.Model);
        var secrets = new MemorySecretStore { BoundValue = old };
        AppSettings? saved = null;
        var coordinator = new AiSettingsSaveCoordinator(
            secrets,
            (settings, _) =>
            {
                saved = settings;
                return Task.CompletedTask;
            });

        var result = await coordinator.ApplyAsync(
            old,
            new AiSettingsSubmission(
                new AppSettings
                {
                    AiEnabled = true,
                    AiEndpoint = "https://NEW.example/v1/",
                    AiModel = "model"
                },
                null,
                false),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(old.BindingId, result.Secret!.BindingId);
        Assert.Equal(result.Secret.BindingId, saved!.AiSecretBindingId);
        Assert.Equal(
            "https://new.example/v1/chat/completions",
            result.Secret.CanonicalEndpoint);
    }

    [Fact]
    public async Task ApplyAsync_设置与补偿均失败时留下不匹配状态并拒绝解析()
    {
        var config = Honey.Integrations.Ai.AiEndpointValidator.StrictValidate(
            "https://old.example/v1",
            "model");
        var old = new BoundAiSecret(
            "old-key", "old-binding", 1, config.CanonicalEndpoint, config.Model);
        var secrets = new MemorySecretStore
        {
            BoundValue = old,
            FailBoundSaveNumber = 2
        };
        var coordinator = new AiSettingsSaveCoordinator(
            secrets,
            (_, _) => throw new InvalidOperationException("模拟设置落盘失败"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.ApplyAsync(
                old,
                new AiSettingsSubmission(
                    new AppSettings
                    {
                        AiEnabled = true,
                        AiEndpoint = "https://new.example/v1",
                        AiModel = "model"
                    },
                    null,
                    false),
                TestContext.Current.CancellationToken));

        Assert.True(exception.Data.Contains("Honey.Ai.SecretCompensation"));
        Assert.NotEqual(old.BindingId, secrets.BoundValue!.BindingId);
        Assert.False(Honey.Integrations.Ai.AiConfigurationResolver.Resolve(
            true,
            "https://old.example/v1",
            "model",
            old.BindingId,
            secrets.BoundValue).Available);
    }

    private sealed class MemorySecretStore : IAiSecretStore
    {
        public BoundAiSecret? BoundValue { get; set; }
        public int? FailBoundSaveNumber { get; set; }
        private int _boundSaveCount;

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BoundValue = null;
            return Task.CompletedTask;
        }

        public Task SaveBoundAsync(
            BoundAiSecret secret,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _boundSaveCount++;
            if (_boundSaveCount == FailBoundSaveNumber)
            {
                throw new IOException("模拟绑定密钥补偿失败");
            }

            BoundValue = secret;
            return Task.CompletedTask;
        }

        public Task<BoundAiSecret?> LoadBoundAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BoundValue);
    }
}
