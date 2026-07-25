using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Honey.Integrations.Ai;

public sealed class OpenAiCompatibleProvider : IAiCompanionProvider
{
    private const int MaximumUserTextLength = 2_000;
    private const int MaximumStateLength = 1_000;
    private const int MaximumMemoryLength = 1_000;
    private const int MaximumRequestTextLength = 8_000;
    private const int MaximumResponseBytes = 64 * 1024;
    private const int MaximumResponseTextLength = 800;
    private static readonly HashSet<string> AllowedIntents =
        new(StringComparer.Ordinal)
        {
            "observe", "play", "sleep", "forage", "web"
        };

    private readonly HttpClient _client;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly TimeSpan _timeout;

    public OpenAiCompatibleProvider(HttpClient client, AiOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(options);
        var validated = AiEndpointValidator.StrictValidate(
            options.BaseEndpoint,
            options.Model);
        _endpoint = validated.ChatCompletionsEndpoint;
        _model = validated.Model;
        _apiKey = string.IsNullOrWhiteSpace(options.ApiKey)
            ? throw new ArgumentException("API 密钥不能为空。", nameof(options))
            : options.ApiKey.Trim();
        _timeout = options.Timeout > TimeSpan.Zero
            ? options.Timeout
            : AiOptions.DefaultTimeout;
    }

    public async Task<AiCompanionResult> CompleteAsync(
        AiCompanionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            using var message = CreateRequest(request);
            using var response = await _client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(MapStatusCode(response.StatusCode));
            }

            using var stream = await response.Content
                .ReadAsStreamAsync(linked.Token)
                .ConfigureAwait(false);
            var bytes = await ReadLimitedAsync(stream, MaximumResponseBytes, linked.Token)
                .ConfigureAwait(false);
            if (bytes is null)
            {
                return Failure("response_too_large");
            }

            return ParseResponse(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failure("timeout");
        }
        catch (HttpRequestException)
        {
            return Failure("network");
        }
        catch (JsonException)
        {
            return Failure("invalid_json");
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException)
        {
            return Failure("network");
        }
    }

    private HttpRequestMessage CreateRequest(AiCompanionRequest request)
    {
        var userText = Limit(request.UserText, MaximumUserTextLength);
        var state = Limit(request.PetStateSummary, MaximumStateLength);
        var memories = (request.MemorySummaries ?? [])
            .Take(8)
            .Select(memory => Limit(memory, MaximumMemoryLength))
            .ToArray();
        var total = userText.Length + state.Length + memories.Sum(value => value.Length);
        if (total > MaximumRequestTextLength)
        {
            var remaining = Math.Max(0, MaximumRequestTextLength - userText.Length - state.Length);
            memories = memories
                .Select(memory =>
                {
                    var limited = memory[..Math.Min(memory.Length, remaining)];
                    remaining -= limited.Length;
                    return limited;
                })
                .Where(memory => memory.Length > 0)
                .ToArray();
        }

        var userPayload = JsonSerializer.Serialize(new
        {
            user_text = userText,
            pet_state_summary = state,
            memory_summaries = memories
        });
        var payload = JsonSerializer.Serialize(new
        {
            model = _model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "你是温和简短的桌面灵宠伙伴。不要声称拥有真实意识，不执行外部动作。只返回紧凑 JSON：{\"text\":\"...\",\"suggested_intent\":\"observe|play|sleep|forage|web|null\"}。"
                },
                new { role = "user", content = userPayload }
            }
        });
        var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        return message;
    }

    private static AiCompanionResult ParseResponse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        if (!document.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String)
        {
            return Failure("invalid_response");
        }

        var raw = content.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Failure("invalid_response");
        }

        JsonDocument inner;
        try
        {
            inner = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return Failure("invalid_response");
        }

        using (inner)
        {
            if (!inner.RootElement.TryGetProperty("text", out var textElement)
                || textElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(textElement.GetString()))
            {
                return Failure("invalid_response");
            }

            var text = Limit(textElement.GetString(), MaximumResponseTextLength);
            string? intent = null;
            if (inner.RootElement.TryGetProperty("suggested_intent", out var intentElement)
                && intentElement.ValueKind == JsonValueKind.String)
            {
                var normalized = intentElement.GetString()?.Trim().ToLowerInvariant();
                intent = normalized is not null && AllowedIntents.Contains(normalized)
                    ? normalized
                    : null;
            }

            return new AiCompanionResult(true, text, intent, null);
        }
    }

    private static async Task<byte[]?> ReadLimitedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximumBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string Limit(string? value, int maximum) =>
        string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, maximum)];

    private static AiCompanionResult Failure(string code) =>
        new(false, null, null, code);

    private static string MapStatusCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "redirect",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "auth",
            HttpStatusCode.TooManyRequests => "rate_limited",
            >= HttpStatusCode.InternalServerError => "server_error",
            _ => "bad_request"
        };
}
