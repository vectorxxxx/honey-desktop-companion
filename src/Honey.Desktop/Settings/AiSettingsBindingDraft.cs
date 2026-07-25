namespace Honey.Desktop.Settings;

public sealed class AiSettingsBindingDraft(string? bindingId)
{
    public string? BindingId { get; private set; } = Normalize(bindingId);

    public void Set(string? bindingId) => BindingId = Normalize(bindingId);

    public void Clear() => BindingId = null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
