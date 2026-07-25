using System.Net;

namespace Honey.Integrations.Ai;

public sealed class AiConfigurationException(string message) : ArgumentException(message);

public sealed record AiValidatedConfiguration(
    string CanonicalEndpoint,
    Uri ChatCompletionsEndpoint,
    string Model);

public static class AiEndpointValidator
{
    public const int MaximumModelLength = 200;

    public static AiValidatedConfiguration StrictValidate(string? endpoint, string? model)
    {
        if (!Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new AiConfigurationException("AI 服务地址无效。");
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !IsExplicitLoopback(uri.Host))
        {
            throw new AiConfigurationException("仅明确的本机服务可使用明文 HTTP。");
        }

        var normalizedModel = model?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel)
            || normalizedModel.Length > MaximumModelLength
            || normalizedModel.Any(char.IsControl))
        {
            throw new AiConfigurationException("模型名称不能为空、含控制字符或超过 200 个字符。");
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            path = $"{path}/chat/completions";
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = host,
            Path = path,
            Query = string.Empty,
            Fragment = string.Empty
        };
        var target = builder.Uri;
        return new AiValidatedConfiguration(
            target.AbsoluteUri.TrimEnd('/'),
            target,
            normalizedModel);
    }

    private static bool IsExplicitLoopback(string host)
    {
        var plain = host.Trim('[', ']').TrimEnd('.');
        if (string.Equals(plain, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(plain, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return IPAddress.IsLoopback(address);
    }
}
