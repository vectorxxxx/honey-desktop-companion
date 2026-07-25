namespace Honey.Integrations.Ai;

public sealed record AiCompanionRequest(
    string UserText,
    string PetStateSummary,
    IReadOnlyList<string> MemorySummaries);

public sealed record AiCompanionResult(
    bool Available,
    string? Text,
    string? SuggestedIntent,
    string? FailureCode);

public interface IAiCompanionProvider
{
    Task<AiCompanionResult> CompleteAsync(
        AiCompanionRequest request,
        CancellationToken cancellationToken);
}

public sealed record AiOptions(
    string BaseEndpoint,
    string Model,
    string ApiKey,
    TimeSpan Timeout)
{
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(15);
}
