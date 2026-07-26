using Honey.Domain.Model;

namespace Honey.Rendering;

public sealed record RenderSnapshot(
    PetMode Mode,
    PetMood Mood,
    float LookX,
    float LookY,
    double AnimationTime,
    float Scale,
    string Behavior,
    string Phase = "",
    double PhaseProgress = 0,
    double SkillProgress = 0,
    float FacingX = 0,
    float FacingY = -1,
    float NormalizedSpeed = 0,
    float StridePhase = 0,
    float TurnLean = 0)
{
    public RenderSnapshot Normalize()
    {
        var facingX = float.IsFinite(FacingX) ? FacingX : 0;
        var facingY = float.IsFinite(FacingY) ? FacingY : -1;
        var facingLength = MathF.Sqrt(facingX * facingX + facingY * facingY);
        if (facingLength <= float.Epsilon)
        {
            facingX = 0;
            facingY = -1;
        }
        else
        {
            facingX /= facingLength;
            facingY /= facingLength;
        }

        return this with
        {
            LookX = float.IsFinite(LookX) ? Math.Clamp(LookX, -1, 1) : 0,
            LookY = float.IsFinite(LookY) ? Math.Clamp(LookY, -1, 1) : 0,
            AnimationTime = double.IsFinite(AnimationTime) ? Math.Max(0, AnimationTime) : 0,
            Scale = float.IsFinite(Scale) && Scale > 0 ? Math.Clamp(Scale, 0.4f, 2f) : 1,
            Behavior = Behavior ?? string.Empty,
            Phase = Phase ?? string.Empty,
            PhaseProgress = double.IsFinite(PhaseProgress) ? Math.Clamp(PhaseProgress, 0, 1) : 0,
            SkillProgress = double.IsFinite(SkillProgress) ? Math.Clamp(SkillProgress, 0, 1) : 0,
            FacingX = facingX,
            FacingY = facingY,
            NormalizedSpeed = float.IsFinite(NormalizedSpeed)
                ? Math.Clamp(NormalizedSpeed, 0, 1)
                : 0,
            StridePhase = float.IsFinite(StridePhase)
                ? Math.Clamp(StridePhase, 0, 1)
                : 0,
            TurnLean = float.IsFinite(TurnLean)
                ? Math.Clamp(TurnLean, -1, 1)
                : 0
        };
    }
}
