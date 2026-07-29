using Honey.Integrations.Ai;

namespace Honey.Desktop.Settings;

public sealed record AppSettings
{
    public const int CurrentSettingsVersion = 1;
    public const int DefaultPetSize = 80;
    public const int LegacyDefaultPetSize = 140;

    public int SettingsVersion { get; init; } = CurrentSettingsVersion;
    public int PetSize { get; init; } = DefaultPetSize;
    public string ActivityLevel { get; init; } = "balanced";
    public string ModePreference { get; init; } = "auto";
    public bool StartWithWindows { get; init; }
    public bool FocusMode { get; init; } = true;
    public bool AutonomousMovementEnabled { get; init; } = true;
    public bool AllowCrossMonitorRoaming { get; init; }
    public bool AiEnabled { get; init; }
    public string AiEndpoint { get; init; } = "https://api.openai.com/v1";
    public string AiModel { get; init; } = "gpt-5.6-luna";
    public string? AiSecretBindingId { get; init; }
    public bool SoundEnabled { get; init; } = true;
    public double SoundVolume { get; init; } = 0.35;

    public AppSettings Normalize()
    {
        var activity = ActivityLevel?.ToLowerInvariant();
        var mode = ModePreference?.ToLowerInvariant();
        var volume = double.IsFinite(SoundVolume) ? Math.Clamp(SoundVolume, 0, 1) : 0.35;
        AiValidatedConfiguration? aiConfiguration = null;
        try
        {
            aiConfiguration = AiEndpointValidator.StrictValidate(AiEndpoint, AiModel);
        }
        catch (AiConfigurationException)
        {
            // 加载旧设置时回退默认值并在下方强制关闭 AI；发送入口仍使用严格校验。
        }

        return this with
        {
            PetSize = Math.Clamp(PetSize, 60, 240),
            ActivityLevel = activity is "quiet" or "balanced" or "active" ? activity : "balanced",
            ModePreference = mode is "auto" or "normal" or "berserk" ? mode : "auto",
            SoundVolume = volume,
            AiEndpoint = aiConfiguration is null
                ? "https://api.openai.com/v1"
                : AiEndpoint.Trim().TrimEnd('/'),
            AiModel = aiConfiguration?.Model ?? "gpt-5.6-luna",
            AiEnabled = AiEnabled && aiConfiguration is not null,
            AiSecretBindingId = aiConfiguration is null ? null : AiSecretBindingId
        };
    }
}
