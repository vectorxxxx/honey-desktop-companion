using Honey.Domain.Model;

namespace Honey.Domain.Movement;

public sealed record PetLocomotionContext(
    string Behavior,
    string Phase,
    PetMood Mood,
    PetMode Mode,
    PointerStimulus Pointer);
