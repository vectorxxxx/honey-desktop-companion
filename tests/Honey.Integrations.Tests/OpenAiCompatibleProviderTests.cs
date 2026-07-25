using System.Net;
using System.Text;
using System.Text.Json;
using Honey.Integrations.Ai;

namespace Honey.Integrations.Tests;

public sealed class OpenAiCompatibleProviderTests
{
    [Fact]
    public async Task CompleteAsync_规范化地址并仅发送许可字段()
    {
        Uri? requestedUri = null;
        string? authorization = null;
        string? body = null;
        using var client = new HttpClient(new StubHandler(async (request, _) =>
        {
            requestedUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            body = await request.Content!.ReadAsStringAsync();
            return ChatResponse("""{"text":"去看看窗边。","suggested_intent":" OBSERVE "}""");
        }));
        var provider = CreateProvider(client, "https://example.com/v1/");

        var result = await provider.CompleteAsync(
            new AiCompanionRequest(
                "陪我看看",
                "好奇；常态；观察",
                Enumerable.Range(1, 12).Select(index => $"记忆{index}").ToArray()),
            CancellationToken.None);

        Assert.Equal("https://example.com/v1/chat/completions", requestedUri!.ToString());
        Assert.Equal("Bearer sk-secret", authorization);
        Assert.True(result.Available, result.FailureCode);
        Assert.Equal("observe", result.SuggestedIntent);
        using var outer = JsonDocument.Parse(body!);
        Assert.Equal("test-model", outer.RootElement.GetProperty("model").GetString());
        Assert.False(outer.RootElement.TryGetProperty("tools", out _));
        var userJson = outer.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        using var user = JsonDocument.Parse(userJson);
        Assert.Equal(8, user.RootElement.GetProperty("memory_summaries").GetArrayLength());
        Assert.Equal(3, user.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task CompleteAsync_内部超时返回不可用而不抛出()
    {
        using var client = new HttpClient(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var provider = new OpenAiCompatibleProvider(
            client,
            new AiOptions("https://example.com/v1", "test-model", "sk-secret", TimeSpan.FromMilliseconds(20)));

        var result = await provider.CompleteAsync(
            new AiCompanionRequest("陪我玩", "好奇；常态；观察", []),
            CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("timeout", result.FailureCode);
    }

    [Fact]
    public async Task CompleteAsync_调用方取消必须传播()
    {
        using var client = new HttpClient(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var provider = CreateProvider(client);
        using var cancellation = new CancellationTokenSource(20);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(
                new AiCompanionRequest("你好", "常态", []),
                cancellation.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "auth")]
    [InlineData(HttpStatusCode.Forbidden, "auth")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, "server_error")]
    [InlineData(HttpStatusCode.BadRequest, "bad_request")]
    [InlineData(HttpStatusCode.Redirect, "redirect")]
    public async Task CompleteAsync_将HTTP失败映射为稳定错误码(
        HttpStatusCode status,
        string failureCode)
    {
        using var client = new HttpClient(new StubHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(status))));
        var result = await CreateProvider(client).CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal(failureCode, result.FailureCode);
    }

    [Fact]
    public async Task CompleteAsync_非法建议被丢弃但文本仍可用()
    {
        using var client = new HttpClient(new StubHandler(
            (_, _) => Task.FromResult(JsonResponse(
                """{"choices":[{"message":{"content":"{\"text\":\"我会陪着你。\",\"suggested_intent\":\"pounce\"}"}}]}"""))));

        var result = await CreateProvider(client).CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("我会陪着你。", result.Text);
        Assert.Null(result.SuggestedIntent);
    }

    [Theory]
    [InlineData("{", "invalid_json")]
    [InlineData("{}", "invalid_response")]
    [InlineData("""{"choices":[{"message":{"content":""}}]}""", "invalid_response")]
    public async Task CompleteAsync_拒绝无效响应(string responseJson, string failureCode)
    {
        using var client = new HttpClient(new StubHandler(
            (_, _) => Task.FromResult(JsonResponse(responseJson))));

        var result = await CreateProvider(client).CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal(failureCode, result.FailureCode);
    }

    [Fact]
    public async Task CompleteAsync_响应超过上限时安全降级()
    {
        var huge = new string('x', 70_000);
        using var client = new HttpClient(new StubHandler(
            (_, _) => Task.FromResult(JsonResponse(huge))));

        var result = await CreateProvider(client).CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.Equal("response_too_large", result.FailureCode);
    }

    [Theory]
    [InlineData(65_536, "invalid_json")]
    [InlineData(65_537, "response_too_large")]
    public async Task CompleteAsync_按实际流字节执行64KiB边界(
        int byteCount,
        string failureCode)
    {
        using var client = new HttpClient(new StubHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new MisleadingLengthContent(
                    Enumerable.Repeat((byte)'x', byteCount).ToArray(),
                    declaredLength: 1)
            })));

        var result = await CreateProvider(client).CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.Equal(failureCode, result.FailureCode);
    }

    [Fact]
    public async Task CompleteAsync_网络异常时安全降级()
    {
        using var client = new HttpClient(new StubHandler(
            (_, _) => throw new HttpRequestException("测试网络故障")));

        var result = await CreateProvider(client).CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.Equal("network", result.FailureCode);
    }

    [Theory]
    [InlineData("http://example.com/v1")]
    [InlineData("ftp://localhost/v1")]
    [InlineData("https://user@example.com/v1")]
    [InlineData("https://example.com/v1?secret=x")]
    public void Constructor_拒绝不安全地址(string endpoint)
    {
        using var client = new HttpClient();
        Assert.ThrowsAny<ArgumentException>(
            () => CreateProvider(client, endpoint));
    }

    [Fact]
    public async Task CompleteAsync_允许本机明文兼容服务且完整路径不重复()
    {
        Uri? requestedUri = null;
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(JsonResponse(
                """{"choices":[{"message":{"content":"{\"text\":\"好。\",\"suggested_intent\":null}"}}]}"""));
        }));
        var provider = CreateProvider(client, "http://127.0.0.1:1234/v1/chat/completions");

        await provider.CompleteAsync(
            new AiCompanionRequest("你好", "常态", []),
            CancellationToken.None);

        Assert.Equal("http://127.0.0.1:1234/v1/chat/completions", requestedUri!.ToString());
    }

    private static OpenAiCompatibleProvider CreateProvider(
        HttpClient client,
        string endpoint = "https://example.com/v1") =>
        new(
            client,
            new AiOptions(
                endpoint,
                "test-model",
                "sk-secret",
                TimeSpan.FromSeconds(1)));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ChatResponse(string content) =>
        JsonResponse(JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        }));

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class MisleadingLengthContent(byte[] bytes, long declaredLength)
        : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = declaredLength;
            return true;
        }
    }
}
