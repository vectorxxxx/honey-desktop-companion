using Honey.Integrations.Ai;
using Honey.Integrations.Security;

namespace Honey.Desktop.Settings;

public sealed record AiSettingsSaveResult(
    AppSettings Settings,
    BoundAiSecret? Secret);

public sealed class AiSettingsSaveCoordinator(
    IAiSecretStore secretStore,
    Func<AppSettings, CancellationToken, Task> saveSettings)
{
    public async Task<AiSettingsSaveResult> ApplyAsync(
        BoundAiSecret? currentSecret,
        AiSettingsSubmission submission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var requested = submission.Settings;

        if (submission.ClearApiKey && string.IsNullOrWhiteSpace(submission.NewApiKey))
        {
            var disabled = requested.Normalize() with
            {
                AiEnabled = false,
                AiSecretBindingId = null
            };
            await saveSettings(disabled, cancellationToken).ConfigureAwait(false);
            await secretStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
            return new AiSettingsSaveResult(disabled, null);
        }

        var apiKey = string.IsNullOrWhiteSpace(submission.NewApiKey)
            ? currentSecret?.ApiKey
            : submission.NewApiKey.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (requested.AiEnabled)
            {
                throw new InvalidOperationException("启用 AI 前必须安全保存 API 密钥。");
            }

            var withoutSecret = requested.Normalize() with { AiSecretBindingId = null };
            await saveSettings(withoutSecret, cancellationToken).ConfigureAwait(false);
            return new AiSettingsSaveResult(withoutSecret, null);
        }

        var validated = AiEndpointValidator.StrictValidate(
            requested.AiEndpoint,
            requested.AiModel);
        var bindingId = Guid.NewGuid().ToString("N");
        var nextSecret = new BoundAiSecret(
            apiKey,
            bindingId,
            BoundAiSecret.CurrentConfigVersion,
            validated.CanonicalEndpoint,
            validated.Model);
        var nextSettings = requested.Normalize() with
        {
            AiEndpoint = requested.AiEndpoint.Trim().TrimEnd('/'),
            AiModel = validated.Model,
            AiSecretBindingId = bindingId
        };

        await secretStore.SaveBoundAsync(nextSecret, cancellationToken).ConfigureAwait(false);
        try
        {
            await saveSettings(nextSettings, cancellationToken).ConfigureAwait(false);
            return new AiSettingsSaveResult(nextSettings, nextSecret);
        }
        catch (Exception original)
        {
            try
            {
                if (currentSecret is null)
                {
                    await secretStore.DeleteAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await secretStore.SaveBoundAsync(currentSecret, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception compensation)
            {
                original.Data["Honey.Ai.SecretCompensation"] = compensation;
            }

            throw;
        }
    }
}
