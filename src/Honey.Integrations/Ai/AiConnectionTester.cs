namespace Honey.Integrations.Ai;

public sealed class AiConnectionTester(AiRequestGate gate)
{
    public async Task<AiCompanionResult> TestAsync(
        IAiCompanionProvider provider,
        AiCompanionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!gate.TryAcquire(out var lease, out var failureCode))
        {
            return new AiCompanionResult(false, null, null, failureCode);
        }

        using (lease)
        {
            try
            {
                return await provider.CompleteAsync(request, cancellationToken)
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
        }
    }
}
