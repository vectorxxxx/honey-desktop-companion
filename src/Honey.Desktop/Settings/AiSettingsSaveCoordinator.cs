using Honey.Integrations.Security;

namespace Honey.Desktop.Settings;

public sealed class AiSettingsSaveCoordinator(
    IAiSecretStore secretStore,
    Func<AppSettings, CancellationToken, Task> saveSettings)
{
    public async Task<string?> ApplyAsync(
        string? currentApiKey,
        AiSettingsSubmission submission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var nextKey = currentApiKey;
        try
        {
            if (!string.IsNullOrWhiteSpace(submission.NewApiKey))
            {
                await secretStore.SaveAsync(submission.NewApiKey, cancellationToken);
                nextKey = submission.NewApiKey;
            }
            else if (submission.ClearApiKey)
            {
                await secretStore.DeleteAsync(cancellationToken);
                nextKey = null;
            }

            if (submission.Settings.AiEnabled && string.IsNullOrWhiteSpace(nextKey))
            {
                throw new InvalidOperationException("启用 AI 前必须安全保存 API 密钥。");
            }

            await saveSettings(submission.Settings, cancellationToken);
            return nextKey;
        }
        catch (Exception original)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentApiKey))
                {
                    await secretStore.DeleteAsync(CancellationToken.None);
                }
                else
                {
                    await secretStore.SaveAsync(currentApiKey, CancellationToken.None);
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
