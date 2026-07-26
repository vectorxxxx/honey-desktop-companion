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
    public void CreateForSnapshot_每条动态腿的三段中点与关节均可命中(
        PetMood mood,
        double animationTime)
    {
        var snapshot = Snapshot(mood, animationTime) with { Scale = 2 };
        var pose = SpiderGeometry.CreatePose(520, 520, snapshot);
        var hitMap = SpiderHitMap.CreateForSnapshot(520, 520, snapshot);

        for (var index = 0; index < pose.Legs.Count; index++)
        {
            var leg = pose.Legs[index];

            Assert.True(
                hitMap.Contains(leg.Hip.X, leg.Hip.Y),
                $"第 {index} 条腿的髋关节应可命中。");
            Assert.True(
                hitMap.Contains(leg.Knee.X, leg.Knee.Y),
                $"第 {index} 条腿的膝关节应可命中。");
            Assert.True(
                hitMap.Contains(
                    (leg.Root.X + leg.Hip.X) / 2,
                    (leg.Root.Y + leg.Hip.Y) / 2),
                $"第 {index} 条腿的根部至髋关节中点应可命中。");
            Assert.True(
                hitMap.Contains(
                    (leg.Hip.X + leg.Knee.X) / 2,
                    (leg.Hip.Y + leg.Knee.Y) / 2),
                $"第 {index} 条腿的髋关节至膝关节中点应可命中。");
            Assert.True(
                hitMap.Contains(
                    (leg.Knee.X + leg.Tip.X) / 2,
                    (leg.Knee.Y + leg.Tip.Y) / 2),
                $"第 {index} 条腿的膝关节至足尖中点应可命中。");
        }
    }

    [Fact]
    public void Create_明显弯折的腿仍命中根部至髋关节中点()
    {
        var abdomen = new OrientedEllipse(
            new SkiaSharp.SKPoint(240, 240),
            12,
            10,
            0);
        var head = new OrientedEllipse(
            new SkiaSharp.SKPoint(240, 220),
            8,
            6,
            0);
        var leg = new SpiderLeg(
            new SkiaSharp.SKPoint(20, 100),
            new SkiaSharp.SKPoint(20, 20),
            new SkiaSharp.SKPoint(100, 100),
            new SkiaSharp.SKPoint(180, 100),
            4,
            SpiderLegLayer.BehindBody);
        var pose = new SpiderPose(
            260,
            260,
            1,
            new SkiaSharp.SKPoint(240, 240),
            abdomen,
            head,
            [leg],
            new SkiaSharp.SKRect(18, 18, 252, 252));
        var hitMap = SpiderHitMap.Create(pose);

        Assert.True(hitMap.Contains(20, 60));
    }

    [Fact]
    public void Create_非法腿宽不会让远处透明区域误命中()
    {
        var abdomen = new OrientedEllipse(
            new SkiaSharp.SKPoint(240, 240),
            12,
            10,
            0);
        var head = new OrientedEllipse(
            new SkiaSharp.SKPoint(240, 220),
            8,
            6,
            0);
        var leg = new SpiderLeg(
            new SkiaSharp.SKPoint(20, 20),
            new SkiaSharp.SKPoint(30, 20),
            new SkiaSharp.SKPoint(40, 20),
            new SkiaSharp.SKPoint(50, 20),
            float.PositiveInfinity,
            SpiderLegLayer.BehindBody);
        var pose = new SpiderPose(
            260,
            260,
            1,
            new SkiaSharp.SKPoint(240, 240),
            abdomen,
            head,
            [leg],
            new SkiaSharp.SKRect(18, 18, 252, 252));

        Assert.False(SpiderHitMap.Create(pose).Contains(180, 180));
    }

    [Fact]
    public void CreateForSnapshot_斜向身体只命中旋转后的真实椭圆()
    {
        var abdomen = new OrientedEllipse(
            new SkiaSharp.SKPoint(130, 130),
            50,
            20,
            MathF.PI / 4);
        var head = new OrientedEllipse(
            new SkiaSharp.SKPoint(85, 85),
            12,
            10,
            MathF.PI / 4);
        var pose = new SpiderPose(
            260,
            260,
            1,
            new SkiaSharp.SKPoint(130, 130),
            abdomen,
            head,
            [],
            abdomen.Bounds);
        var hitMap = SpiderHitMap.Create(pose);

        Assert.True(hitMap.Contains(pose.Abdomen.Center.X, pose.Abdomen.Center.Y));
        Assert.False(hitMap.Contains(
            pose.Abdomen.Bounds.Right + 8,
            pose.Abdomen.Bounds.Top - 8));
    }

    private static RenderSnapshot Snapshot(PetMood mood, double time) =>
        new(PetMode.Normal, mood, 0, 0, time, 1, "observe");
}
