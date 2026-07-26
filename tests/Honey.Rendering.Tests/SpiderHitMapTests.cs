using Honey.Rendering.Spider;
using Honey.Domain.Model;

namespace Honey.Rendering.Tests;

public sealed class SpiderHitMapTests
{
    [Fact]
    public void Contains_命中腹部但不命中透明角落()
    {
        var hitMap = SpiderHitMap.CreateDefault(200, 200, 1);

        Assert.True(hitMap.Contains(100, 100));
        Assert.False(hitMap.Contains(10, 10));
        Assert.False(hitMap.Contains(100, 25));
    }

    [Theory]
    [InlineData(0.6f)]
    [InlineData(1.0f)]
    [InlineData(1.6f)]
    public void Contains_不同缩放下腿部与身体保持可命中(float scale)
    {
        var hitMap = SpiderHitMap.CreateDefault(240, 240, scale);
        var layout = SpiderLayout.Create(240, 240, scale);
        var leg = layout.Legs[0];
        var legMidX = (leg.Root.X + leg.Knee.X) / 2;
        var legMidY = (leg.Root.Y + leg.Knee.Y) / 2;

        Assert.True(hitMap.Contains(layout.Center.X, layout.Center.Y));
        Assert.True(hitMap.Contains(legMidX, legMidY));
        Assert.False(hitMap.Contains(2, 2));
    }

    [Fact]
    public void Contains_小尺寸腿部两侧保留最小抓取容差()
    {
        const float scale = 0.45f;
        var hitMap = SpiderHitMap.CreateDefault(220, 220, scale);
        var layout = SpiderLayout.Create(220, 220, scale);
        var leg = layout.Legs[0];
        var midpoint = new SkiaSharp.SKPoint(
            (leg.Root.X + leg.Knee.X) / 2,
            (leg.Root.Y + leg.Knee.Y) / 2);
        var segmentX = leg.Knee.X - leg.Root.X;
        var segmentY = leg.Knee.Y - leg.Root.Y;
        var length = MathF.Sqrt(segmentX * segmentX + segmentY * segmentY);
        var offsetX = -segmentY / length * 6;
        var offsetY = segmentX / length * 6;

        Assert.True(hitMap.Contains(midpoint.X + offsetX, midpoint.Y + offsetY));
    }

    [Fact]
    public void Contains_身体轮廓外侧仍可抓取但远处保持透明()
    {
        var hitMap = SpiderHitMap.CreateDefault(240, 240, 0.6f);
        var layout = SpiderLayout.Create(240, 240, 0.6f);

        Assert.True(hitMap.Contains(
            layout.Abdomen.Right + 6,
            layout.Abdomen.MidY));
        Assert.False(hitMap.Contains(
            layout.Abdomen.Right + 30,
            layout.Abdomen.MidY));
    }

    [Fact]
    public void CreateDefault_非法尺寸生成空命中图()
    {
        Assert.False(SpiderHitMap.CreateDefault(0, 200, 1).Contains(0, 0));
        Assert.False(SpiderHitMap.CreateDefault(-1, 200, 1).Contains(0, 0));
        Assert.False(SpiderHitMap.CreateDefault(float.NaN, 200, 1).Contains(0, 0));
    }

    [Fact]
    public void CreateDefault_非法缩放回退到一倍且非法点不命中()
    {
        var normal = SpiderHitMap.CreateDefault(200, 200, 1);
        var invalid = SpiderHitMap.CreateDefault(200, 200, float.NaN);

        Assert.Equal(
            normal.Contains(100, 100),
            invalid.Contains(100, 100));
        Assert.False(invalid.Contains(float.NaN, 100));
        Assert.False(invalid.Contains(100, float.PositiveInfinity));
    }

    [Fact]
    public void CreateForSnapshot_只命中当前动画时刻的腿部姿态()
    {
        const float width = 240;
        const float height = 240;
        var firstTime = Math.PI / (2 * 3.1);
        var laterTime = 3 * Math.PI / (2 * 3.1);
        var first = Snapshot(PetMood.Alert, firstTime);
        var later = Snapshot(PetMood.Alert, laterTime);
        var firstPose = SpiderGeometry.CreatePose(width, height, first);
        var firstKnee = firstPose.Legs[0].Knee;

        Assert.True(SpiderHitMap.CreateForSnapshot(width, height, first).Contains(firstKnee.X, firstKnee.Y));
        Assert.False(SpiderHitMap.CreateForSnapshot(width, height, later).Contains(firstKnee.X, firstKnee.Y));
    }

    [Theory]
    [InlineData(PetMood.Happy, 0.25)]
    [InlineData(PetMood.Curious, 0.75)]
    [InlineData(PetMood.Hungry, 1.25)]
    [InlineData(PetMood.Sleepy, 1.75)]
    [InlineData(PetMood.Alert, 2.25)]
    [InlineData(PetMood.Angry, 2.75)]
    public void CreateForSnapshot_每条动态腿的两段中点与膝点均可命中(
        PetMood mood,
        double animationTime)
    {
        var snapshot = Snapshot(mood, animationTime);
        var pose = SpiderGeometry.CreatePose(260, 260, snapshot);
        var hitMap = SpiderHitMap.CreateForSnapshot(260, 260, snapshot);

        foreach (var leg in pose.Legs)
        {
            Assert.True(hitMap.Contains(leg.Knee.X, leg.Knee.Y));
            Assert.True(hitMap.Contains(
                (leg.Root.X + leg.Knee.X) / 2,
                (leg.Root.Y + leg.Knee.Y) / 2));
            Assert.True(hitMap.Contains(
                (leg.Knee.X + leg.Tip.X) / 2,
                (leg.Knee.Y + leg.Tip.Y) / 2));
        }
    }

    private static RenderSnapshot Snapshot(PetMood mood, double time) =>
        new(PetMode.Normal, mood, 0, 0, time, 1, "observe");
}
