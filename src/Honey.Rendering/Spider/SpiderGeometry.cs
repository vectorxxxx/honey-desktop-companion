using Honey.Domain.Model;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed record SpiderPose(
    float ViewportWidth,
    float ViewportHeight,
    SKPoint Center,
    SKRect Abdomen,
    SKRect Head,
    IReadOnlyList<SpiderLeg> Legs);

public static class SpiderGeometry
{
    public static SpiderPose CreatePose(float width, float height, RenderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var safe = snapshot.Normalize();
        var layout = SpiderLayout.Create(width, height, safe.Scale);
        var cadence = safe.Mode == PetMode.Berserk ? 5.4 : 3.1;
        var moodAmplitude = safe.Mood switch
        {
            PetMood.Sleepy => 0.25f,
            PetMood.Alert or PetMood.Angry => 1.25f,
            PetMood.Happy => 0.8f,
            _ => 0.6f
        };
        var legs = new SpiderLeg[layout.Legs.Count];
        for (var index = 0; index < layout.Legs.Count; index++)
        {
            var leg = layout.Legs[index];
            var phase = safe.AnimationTime * cadence + index * Math.PI / 2;
            var lift = MathF.Sin((float)phase) * leg.Width * moodAmplitude;
            legs[index] = leg with
            {
                Knee = new SKPoint(leg.Knee.X, leg.Knee.Y + lift),
                Tip = new SKPoint(leg.Tip.X, leg.Tip.Y - lift * 0.35f)
            };
        }

        return new SpiderPose(
            width,
            height,
            layout.Center,
            layout.Abdomen,
            layout.Head,
            Array.AsReadOnly(legs));
    }
}
