using Honey.Integrations.Security;

namespace Honey.Integrations.Ai;

public sealed record AiResolvedConfiguration(
    bool Available,
    AiOptions? Options,
    string? FailureCode);

public static class AiConfigurationResolver
{
    public static AiResolvedConfiguration Resolve(
        bool enabled,
        string? endpoint,
        string? model,
        string? settingsBindingId,
        BoundAiSecret? secret)
    {
        if (!enabled)
        {
            return new(false, null, "disabled");
        }

        AiValidatedConfiguration validated;
        try
        {
            validated = AiEndpointValidator.StrictValidate(endpoint, model);
        }
        catch (AiConfigurationException)
        {
            return new(false, null, "validation");
        }

        if (secret is null
            || string.IsNullOrWhiteSpace(settingsBindingId)
            || !string.Equals(settingsBindingId, secret.BindingId, StringComparison.Ordinal)
            || secret.ConfigVersion != BoundAiSecret.CurrentConfigVersion
            || !string.Equals(
                validated.CanonicalEndpoint,
                secret.CanonicalEndpoint,
                StringComparison.Ordinal)
            || !string.Equals(validated.Model, secret.Model, StringComparison.Ordinal))
        {
            return new(false, null, "binding_mismatch");
        }

        return new(
            true,
            new AiOptions(
                validated.CanonicalEndpoint,
                validated.Model,
                secret.ApiKey,
                AiOptions.DefaultTimeout),
            null);
    }
}
