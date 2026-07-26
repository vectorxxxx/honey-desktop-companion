namespace Honey.Rendering.Spider;

public sealed class SpiderHitMap
{
    private const float MinimumGrabRadius = 7;
    private readonly SpiderPose? _pose;
    private readonly float _grabPadding;

    private SpiderHitMap(SpiderPose? pose, float grabPadding = 0)
    {
        _pose = pose;
        _grabPadding = Math.Max(0, grabPadding);
    }

    public static SpiderHitMap CreateDefault(float width, float height, float scale)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
        {
            return new SpiderHitMap(null);
        }

        return CreateForSnapshot(
            width,
            height,
            new RenderSnapshot(
                Honey.Domain.Model.PetMode.Normal,
                Honey.Domain.Model.PetMood.Curious,
                0,
                0,
                0,
                scale,
                string.Empty));
    }

    public static SpiderHitMap CreateForSnapshot(
        float width,
        float height,
        RenderSnapshot snapshot,
        float deviceScale = 1)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
        {
            return new SpiderHitMap(null);
        }

        var pose = SpiderGeometry.CreatePose(width, height, snapshot, deviceScale);
        return new SpiderHitMap(
            pose,
            MinimumGrabRadius * pose.DeviceScale);
    }

    public static SpiderHitMap Create(SpiderPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return new SpiderHitMap(
            pose,
            MinimumGrabRadius * pose.DeviceScale);
    }

    public bool Contains(float x, float y)
    {
        if (_pose is null || !float.IsFinite(x) || !float.IsFinite(y))
        {
            return false;
        }

        var point = new SkiaSharp.SKPoint(x, y);
        if (_pose.Abdomen.Contains(point, _grabPadding)
            || _pose.Head.Contains(point))
        {
            return true;
        }

        return _pose.Legs.Any(leg =>
            DistanceToSegment(x, y, leg.Root.X, leg.Root.Y, leg.Knee.X, leg.Knee.Y)
                <= Math.Max(leg.Width / 2, _grabPadding)
            || DistanceToSegment(x, y, leg.Knee.X, leg.Knee.Y, leg.Tip.X, leg.Tip.Y)
                <= Math.Max(leg.Width / 2, _grabPadding));
    }

    private static float DistanceToSegment(
        float x,
        float y,
        float startX,
        float startY,
        float endX,
        float endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var lengthSquared = deltaX * deltaX + deltaY * deltaY;
        if (lengthSquared <= float.Epsilon)
        {
            return MathF.Sqrt((x - startX) * (x - startX) + (y - startY) * (y - startY));
        }

        var t = Math.Clamp(((x - startX) * deltaX + (y - startY) * deltaY) / lengthSquared, 0, 1);
        var closestX = startX + t * deltaX;
        var closestY = startY + t * deltaY;
        return MathF.Sqrt((x - closestX) * (x - closestX) + (y - closestY) * (y - closestY));
    }
}
