using Honey.Integrations.Security;

namespace Honey.Integrations.Tests;

public sealed class DpapiSecretStoreTests
{
    [Fact]
    public void Protect_当前用户范围往返且密文不含明文()
    {
        const string secret = "sk-secret-for-tests";

        var cipherText = DpapiSecretStore.Protect(secret);

        Assert.DoesNotContain(secret, cipherText, StringComparison.Ordinal);
        Assert.Equal(secret, DpapiSecretStore.Unprotect(cipherText));
    }

    [Theory]
    [InlineData("")]
    [InlineData("不是Base64")]
    public void Unprotect_拒绝空值或错误密文(string cipherText)
    {
        Assert.ThrowsAny<Exception>(() => DpapiSecretStore.Unprotect(cipherText));
    }

    [Fact]
    public async Task SaveLoadDeleteAsync_文件不含明文并支持删除()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"honey-secret-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "secrets.json");
        var config = Honey.Integrations.Ai.AiEndpointValidator.StrictValidate(
            "https://example.com/v1",
            "model");
        var secret = new BoundAiSecret(
            "sk-file-secret",
            "binding",
            BoundAiSecret.CurrentConfigVersion,
            config.CanonicalEndpoint,
            config.Model);
        try
        {
            var store = new DpapiSecretStore(path);

            await store.SaveBoundAsync(secret, TestContext.Current.CancellationToken);

            Assert.DoesNotContain(
                secret.ApiKey,
                await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
            Assert.Equal(secret, await store.LoadBoundAsync(TestContext.Current.CancellationToken));
            await store.DeleteAsync(TestContext.Current.CancellationToken);
            Assert.Null(await store.LoadBoundAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_已取消时不创建文件()
    {
        var path = Path.Combine(Path.GetTempPath(), $"honey-secret-{Guid.NewGuid():N}", "secrets.json");
        var store = new DpapiSecretStore(path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveBoundAsync(
                new BoundAiSecret(
                    "sk-cancelled",
                    "binding",
                    1,
                    "https://example.com/v1/chat/completions",
                    "model"),
                cancellation.Token));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SaveBoundAsync_外层不泄露密钥或端点且绑定往返()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"honey-bound-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "secrets.json");
        var secret = new BoundAiSecret(
            "sk-bound-secret",
            "binding-id",
            BoundAiSecret.CurrentConfigVersion,
            "https://private.example/v1/chat/completions",
            "private-model");
        try
        {
            var store = new DpapiSecretStore(path);
            await store.SaveBoundAsync(secret, TestContext.Current.CancellationToken);
            var file = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

            Assert.DoesNotContain(secret.ApiKey, file, StringComparison.Ordinal);
            Assert.DoesNotContain(secret.CanonicalEndpoint, file, StringComparison.Ordinal);
            Assert.DoesNotContain(secret.Model, file, StringComparison.Ordinal);
            Assert.Equal(secret, await store.LoadBoundAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
