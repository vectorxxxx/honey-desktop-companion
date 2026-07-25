namespace Honey.Desktop.Settings;

public sealed record AppSettings
{
    public int PetSize { get; init; } = 140;
    public string ActivityLevel { get; init; } = "balanced";
    public string ModePreference { get; init; } = "auto";
    public bool StartWithWindows { get; init; }
    public bool FocusMode { get; init; } = true;
    public bool AiEnabled { get; init; }
    public bool SoundEnabled { get; init; } = true;
    public double SoundVolume { get; init; } = 0.35;

    public AppSettings Normalize()
    {
        var activity = ActivityLevel?.ToLowerInvariant();
        var mode = ModePreference?.ToLowerInvariant();
        var volume = double.IsFinite(SoundVolume) ? Math.Clamp(SoundVolume, 0, 1) : 0.35;
        return this with
        {
            PetSize = Math.Clamp(PetSize, 60, 240),
            ActivityLevel = activity is "quiet" or "balanced" or "active" ? activity : "balanced",
            ModePreference = mode is "auto" or "normal" or "berserk" ? mode : "auto",
            SoundVolume = volume
        };
    }
}
