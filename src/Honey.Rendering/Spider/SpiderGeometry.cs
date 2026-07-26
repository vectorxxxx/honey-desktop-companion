using Honey.Domain.Model;
using SkiaSharp;

namespace Honey.Rendering.Spider;

public sealed record SpiderPose(
    float ViewportWidth,
    float ViewportHeight,
    float DeviceScale,
    SKPoint Center,
    OrientedEllipse Abdomen,
    OrientedEllipse Head,
    IReadOnlyList<SpiderLeg> Legs,
    SKRect ContentBounds);

public static class SpiderGeometry
{
    public static SpiderPose CreatePose(
        float width,
        float height,
        RenderSnapshot snapshot,
        float deviceScale = 1)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var safe = snapshot.Normalize();
        var safeDeviceScale = SpiderViewportMetrics.NormalizeDeviceScale(deviceScale);
        var layout = SpiderLayout.Create(width, height, safe.Scale, safeDeviceScale);
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
            var moving = safe.NormalizedSpeed > 0.01f;
            var gaitParity = ((index % 4) + (index / 4)) % 2;
            var phase = moving
                ? safe.StridePhase * MathF.Tau + gaitParity * MathF.PI
                : (float)(safe.AnimationTime * cadence + index * Math.PI / 2);
            var amplitude = moving
                ? leg.Width * moodAmplitude * (1.2f + safe.NormalizedSpeed * 1.8f)
                : leg.Width * moodAmplitude * 1.25f;
            var lift = MathF.Sin(phase) * amplitude;
            var sweep = moving ? MathF.Cos(phase) * amplitude * 0.35f : 0;
            var hipLift = lift * 0.16f;
            var kneeLift = lift * 0.52f;
            var tipLift = lift;
            var hipSweep = sweep * 0.22f;
            var kneeSweep = sweep * 0.66f;
            legs[index] = leg with
            {
                Hip = new SKPoint(leg.Hip.X + hipSweep, leg.Hip.Y + hipLift),
                Knee = new SKPoint(leg.Knee.X + kneeSweep, leg.Knee.Y + kneeLift),
                Tip = new SKPoint(leg.Tip.X + sweep, leg.Tip.Y + tipLift)
            };
        }

        var rotation = MathF.Atan2(safe.FacingY, safe.FacingX) + MathF.PI / 2;
        for (var index = 0; index < legs.Length; index++)
        {
            var leg = legs[index];
            legs[index] = leg with
            {
                Root = Rotate(leg.Root, layout.Center, rotation),
                Hip = Rotate(leg.Hip, layout.Center, rotation),
                Knee = Rotate(leg.Knee, layout.Center, rotation),
                Tip = Rotate(leg.Tip, layout.Center, rotation)
            };
        }
        var abdomen = CreateBodyPart(layout.Abdomen, layout.Center, rotation);
        var head = CreateBodyPart(layout.Head, layout.Center, rotation);
        return new SpiderPose(
            width,
            height,
            safeDeviceScale,
            layout.Center,
            abdomen,
            head,
            legs,
            CalculateContentBounds(abdomen, head, legs));
    }

    private static SKPoint Rotate(SKPoint point, SKPoint center, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        var x = point.X - center.X;
        var y = point.Y - center.Y;
        return new SKPoint(
            center.X + x * cosine - y * sine,
            center.Y + x * sine + y * cosine);
    }

    private static OrientedEllipse CreateBodyPart(
        SKRect rectangle,
        SKPoint center,
        float rotation)
    {
        var rotatedCenter = Rotate(
            new SKPoint(rectangle.MidX, rectangle.MidY),
            center,
            rotation);
        return new OrientedEllipse(
            rotatedCenter,
            rectangle.Width / 2,
            rectangle.Height / 2,
            rotation);
    }

    private static SKRect CalculateContentBounds(
        OrientedEllipse abdomen,
        OrientedEllipse head,
        IReadOnlyList<SpiderLeg> legs)
    {
        var left = Math.Min(abdomen.Bounds.Left, head.Bounds.Left);
        var top = Math.Min(abdomen.Bounds.Top, head.Bounds.Top);
        var right = Math.Max(abdomen.Bounds.Right, head.Bounds.Right);
        var bottom = Math.Max(abdomen.Bounds.Bottom, head.Bounds.Bottom);
        foreach (var leg in legs)
        {
            var radius = leg.Width * 1.18f / 2;
            Include(leg.Root, radius, ref left, ref top, ref right, ref bottom);
            Include(leg.Hip, radius, ref left, ref top, ref right, ref bottom);
            Include(leg.Knee, radius, ref left, ref top, ref right, ref bottom);
            Include(leg.Tip, radius, ref left, ref top, ref right, ref bottom);
        }

        return new SKRect(left, top, right, bottom);
    }

    private static void Include(
        SKPoint point,
        float radius,
        ref float left,
        ref float top,
        ref float right,
        ref float bottom)
    {
        left = Math.Min(left, point.X - radius);
        top = Math.Min(top, point.Y - radius);
        right = Math.Max(right, point.X + radius);
        bottom = Math.Max(bottom, point.Y + radius);
    }
}
