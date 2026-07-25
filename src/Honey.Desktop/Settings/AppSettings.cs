namespace Honey.Desktop.Settings;

public sealed record AppSettings
{
    public int PetSize { get; init; } = 140;
    public string ActivityLevel { get; init; } = "balanced";
    public string ModePreference { get; init; } = "auto";
    public bool StartWithWindows { get; init; }
    public bool FocusMode { get; init; } = true;
    public bool AiEnabled { get; init; }
    public string AiEndpoint { get; init; } = "https://api.openai.com/v1";
    public string AiModel { get; init; } = "gpt-5.6-luna";
    public bool SoundEnabled { get; init; } = true;
    public double SoundVolume { get; init; } = 0.35;

    public AppSettings Normalize()
    {
        var activity = ActivityLevel?.ToLowerInvariant();
        var mode = ModePreference?.ToLowerInvariant();
        var volume = double.IsFinite(SoundVolume) ? Math.Clamp(SoundVolume, 0, 1) : 0.35;
        var endpoint = NormalizeAiEndpoint(AiEndpoint);
        var model = string.IsNullOrWhiteSpace(AiModel) || AiModel.Trim().Length > 200
            ? "gpt-5.6-luna"
            : AiModel.Trim();
        var aiConfigurationValid = endpoint is not null
            && !string.IsNullOrWhiteSpace(AiModel)
            && AiModel.Trim().Length <= 200;
        return this with
        {
            PetSize = Math.Clamp(PetSize, 60, 240),
            ActivityLevel = activity is "quiet" or "balanced" or "active" ? activity : "balanced",
            ModePreference = mode is "auto" or "normal" or "berserk" ? mode : "auto",
            SoundVolume = volume,
            AiEndpoint = endpoint ?? "https://api.openai.com/v1",
            AiModel = model,
            AiEnabled = AiEnabled && aiConfigurationValid
        };
    }

    private static string? NormalizeAiEndpoint(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback))
        {
            return null;
        }

        return uri.ToString().TrimEnd('/');
    }
}
