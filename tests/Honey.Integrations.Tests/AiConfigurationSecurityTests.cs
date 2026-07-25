using Honey.Integrations.Ai;
using Honey.Integrations.Security;

namespace Honey.Integrations.Tests;

public sealed class AiConfigurationSecurityTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("http://example.com/v1")]
    [InlineData("https://user@example.com/v1")]
    [InlineData("https://example.com/v1?q=x")]
    [InlineData("https://example.com/v1#x")]
    public void StrictValidate_拒绝危险地址(string endpoint)
    {
        Assert.Throws<AiConfigurationException>(
            () => AiEndpointValidator.StrictValidate(endpoint, "model"));
    }

    [Theory]
    [InlineData("http://localhost/v1")]
    [InlineData("http://localhost./v1/")]
    [InlineData("http://127.8.7.6:1234/v1")]
    [InlineData("http://[::1]:1234/v1")]
    [InlineData("http://[::ffff:127.0.0.1]:1234/v1")]
    public void StrictValidate_允许明确的本机HTTP(string endpoint)
    {
        var result = AiEndpointValidator.StrictValidate(endpoint, " model ");
        Assert.Equal("model", result.Model);
        Assert.EndsWith("/chat/completions", result.CanonicalEndpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolver_只有绑定标识端点模型全部一致才可用()
    {
        var config = AiEndpointValidator.StrictValidate("https://EXAMPLE.com/v1/", "model");
        var secret = new BoundAiSecret("sk-secret", "binding-a", 1, config.CanonicalEndpoint, "model");

        Assert.True(AiConfigurationResolver.Resolve(
            true, "https://example.com/v1", "model", "binding-a", secret).Available);
        Assert.False(AiConfigurationResolver.Resolve(
            true, "https://other.example/v1", "model", "binding-a", secret).Available);
        Assert.False(AiConfigurationResolver.Resolve(
            true, "https://example.com/v1", "other", "binding-a", secret).Available);
        Assert.False(AiConfigurationResolver.Resolve(
            true, "https://example.com/v1", "model", "binding-b", secret).Available);
    }
}
