namespace Honey.Integrations.Ai;

public sealed class AiCompanionCoordinator(Func<IAiCompanionProvider?> providerFactory)
{
    private static readonly HashSet<string> AllowedIntents =
        new(StringComparer.Ordinal)
        {
            "observe", "play", "sleep", "forage", "web"
        };
    private int _requestInFlight;

    public async Task<AiCompanionResult> RequestAsync(
        AiCompanionRequest request,
        Action<string> routeSuggestedIntent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(routeSuggestedIntent);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _requestInFlight, 1, 0) != 0)
        {
            return new AiCompanionResult(false, null, null, "busy");
        }

        try
        {
            var provider = providerFactory();
            if (provider is null)
            {
                return new AiCompanionResult(false, null, null, "disabled");
            }

            AiCompanionResult result;
            try
            {
                result = await provider.CompleteAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return new AiCompanionResult(false, null, null, "provider_error");
            }

            var intent = result.SuggestedIntent?.Trim().ToLowerInvariant();
            intent = intent is not null && AllowedIntents.Contains(intent) ? intent : null;
            if (result.Available && intent is not null)
            {
                try
                {
                    routeSuggestedIntent(intent);
                }
                catch
                {
                    // AI 只是建议；本地运行时拒绝或故障不得破坏可用文本。
                }
            }

            return result with { SuggestedIntent = intent };
        }
        finally
        {
            Volatile.Write(ref _requestInFlight, 0);
        }
    }
}
