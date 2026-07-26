namespace Honey.Desktop.Status;

public sealed record PetNeedGauge(
    string Key,
    string Name,
    int Value,
    bool HighIsGood,
    string Description);
