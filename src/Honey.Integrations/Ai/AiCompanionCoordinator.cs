namespace Honey.Integrations.Ai;

public sealed class AiCompanionCoordinator
{
    private static readonly HashSet<string> AllowedIntents =
        new(StringComparer.Ordinal)
        {
            "observe", "play", "sleep", "forage", "web"
        };
    private readonly Func<IAiCompanionProvider?> _providerFactory;
    private readonly AiRequestGate _gate;

    public AiCompanionCoordinator(
        Func<IAiCompanionProvider?> providerFactory,
        AiRequestGate? gate = null)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _gate = gate ?? new AiRequestGate();
    }

    public async Task<AiCompanionResult> RequestAsync(
        AiCompanionRequest request,
        Action<string> routeSuggestedIntent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(routeSuggestedIntent);
        cancellationToken.ThrowIfCancellationRequested();
        var provider = _providerFactory();
        if (provider is null)
        {
            return new AiCompanionResult(false, null, null, "disabled");
        }

        if (!_gate.TryAcquire(out var lease, out var failureCode))
        {
            return new AiCompanionResult(false, null, null, failureCode);
        }

        using (lease)
        {
            AiCompanionResult result;
            try
            {
                result = await provider.CompleteAsync(request, cancellationToken)
                    .ConfigureAwait(false);
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
    }
}
