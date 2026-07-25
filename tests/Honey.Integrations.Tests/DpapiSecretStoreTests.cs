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
        const string secret = "sk-file-secret";
        try
        {
            var store = new DpapiSecretStore(path);

            await store.SaveAsync(secret, TestContext.Current.CancellationToken);

            Assert.DoesNotContain(
                secret,
                await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
            Assert.Equal(secret, await store.LoadAsync(TestContext.Current.CancellationToken));
            await store.DeleteAsync(TestContext.Current.CancellationToken);
            Assert.Null(await store.LoadAsync(TestContext.Current.CancellationToken));
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
            () => store.SaveAsync("sk-cancelled", cancellation.Token));

        Assert.False(File.Exists(path));
    }
}
