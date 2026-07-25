using Honey.Domain.Model;

namespace Honey.Rendering;

public sealed record RenderSnapshot(
    PetMode Mode,
    PetMood Mood,
    float LookX,
    float LookY,
    double AnimationTime,
    float Scale,
    string Behavior)
{
    public RenderSnapshot Normalize() =>
        this with
        {
            LookX = float.IsFinite(LookX) ? Math.Clamp(LookX, -1, 1) : 0,
            LookY = float.IsFinite(LookY) ? Math.Clamp(LookY, -1, 1) : 0,
            AnimationTime = double.IsFinite(AnimationTime) ? Math.Max(0, AnimationTime) : 0,
            Scale = float.IsFinite(Scale) && Scale > 0 ? Math.Clamp(Scale, 0.4f, 2f) : 1,
            Behavior = Behavior ?? string.Empty
        };
}
