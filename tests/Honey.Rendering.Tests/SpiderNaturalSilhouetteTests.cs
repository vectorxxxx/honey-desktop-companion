using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderNaturalSilhouetteTests
{
    [Theory]
    [InlineData(60f)]
    [InlineData(90f)]
    [InlineData(140f)]
    [InlineData(240f)]
    public void Create_各尺寸每条腿均有自然髋膝折角(float petPixels)
    {
        var layout = SpiderLayout.Create(320, 320, petPixels / 140f);

        foreach (var leg in layout.Legs)
        {
            var hipTurn = TurnDegrees(leg.Root, leg.Hip, leg.Knee);
            var kneeTurn = TurnDegrees(leg.Hip, leg.Knee, leg.Tip);

            Assert.InRange(Math.Max(hipTurn, kneeTurn), 22f, 85f);
        }
    }

    [Fact]
    public void Create_紧凑尺寸在六十与九十像素间平滑改变第二条腿膝点()
    {
        var at60 = SpiderLayout.Create(320, 320, 60f / 140f);
        var at90 = SpiderLayout.Create(320, 320, 90f / 140f);
        var below90 = SpiderLayout.Create(320, 320, 89.9f / 140f);
        var above90 = SpiderLayout.Create(320, 320, 90.1f / 140f);

        Assert.True(
            NormalizedDistance(at60, at60.Legs[1].Knee, at90, at90.Legs[1].Knee) >= 0.04f);
        Assert.True(
            NormalizedDistance(
                below90,
                below90.Legs[1].Knee,
                above90,
                above90.Legs[1].Knee) <= 0.01f);
    }

    [Fact]
    public void CreatePose_最小尺寸警觉步态保持折角与同侧足序()
    {
        foreach (var stridePhase in Enumerable.Range(0, 64).Select(index => index / 64f))
        {
            var pose = SpiderGeometry.CreatePose(
                320,
                320,
                new RenderSnapshot(
                    PetMode.Normal,
                    PetMood.Alert,
                    0,
                    -1,
                    0,
                    60f / 140f,
                    "测试") with
                {
                    NormalizedSpeed = 1,
                    StridePhase = stridePhase
                });

            foreach (var leg in pose.Legs)
            {
                Assert.InRange(
                    Math.Max(
                        TurnDegrees(leg.Root, leg.Hip, leg.Knee),
                        TurnDegrees(leg.Hip, leg.Knee, leg.Tip)),
                    18f,
                    85f);
            }

            AssertSameSideOrder(pose.Legs.Take(4), stridePhase, "左侧");
            AssertSameSideOrder(pose.Legs.Skip(4), stridePhase, "右侧");
        }
    }

    [Fact]
    public void CreatePose_十六方向保持髋膝关节转角()
    {
        var up = SpiderGeometry.CreatePose(
            320,
            320,
            new RenderSnapshot(
                PetMode.Normal,
                PetMood.Curious,
                0,
                -1,
                0,
                1,
                "测试"));

        for (var directionIndex = 0; directionIndex < SpiderDirection.Count; directionIndex++)
        {
            var angle = directionIndex * MathF.Tau / SpiderDirection.Count;
            var pose = SpiderGeometry.CreatePose(
                320,
                320,
                new RenderSnapshot(
                    PetMode.Normal,
                    PetMood.Curious,
                    MathF.Sin(angle),
                    -MathF.Cos(angle),
                    0,
                    1,
                    "测试"));

            for (var legIndex = 0; legIndex < pose.Legs.Count; legIndex++)
            {
                var expected = up.Legs[legIndex];
                var actual = pose.Legs[legIndex];
                Assert.Equal(
                    TurnDegrees(expected.Root, expected.Hip, expected.Knee),
                    TurnDegrees(actual.Root, actual.Hip, actual.Knee),
                    3);
                Assert.Equal(
                    TurnDegrees(expected.Hip, expected.Knee, expected.Tip),
                    TurnDegrees(actual.Hip, actual.Knee, actual.Tip),
                    3);
            }
        }
    }

    private static float TurnDegrees(SKPoint start, SKPoint joint, SKPoint end)
    {
        var first = UnitVector(start, joint);
        var second = UnitVector(joint, end);
        var dot = Math.Clamp(first.X * second.X + first.Y * second.Y, -1f, 1f);
        return MathF.Acos(dot) * 180f / MathF.PI;
    }

    private static float NormalizedDistance(
        SpiderLayout firstLayout,
        SKPoint first,
        SpiderLayout secondLayout,
        SKPoint second)
    {
        var firstUnit = firstLayout.Abdomen.Width / 1.52f;
        var secondUnit = secondLayout.Abdomen.Width / 1.52f;
        var normalizedFirst = new SKPoint(
            (first.X - firstLayout.Center.X) / firstUnit,
            (first.Y - firstLayout.Center.Y) / firstUnit);
        var normalizedSecond = new SKPoint(
            (second.X - secondLayout.Center.X) / secondUnit,
            (second.Y - secondLayout.Center.Y) / secondUnit);
        return Distance(normalizedFirst, normalizedSecond);
    }

    private static void AssertSameSideOrder(
        IEnumerable<SpiderLeg> sideLegs,
        float stridePhase,
        string side)
    {
        var legs = sideLegs.ToArray();
        for (var index = 0; index < legs.Length - 1; index++)
        {
            var current = legs[index];
            var next = legs[index + 1];
            Assert.True(
                current.Hip.Y < next.Hip.Y
                    && current.Knee.Y < next.Knee.Y
                    && current.Tip.Y < next.Tip.Y,
                $"{side}第 {index + 1}/{index + 2} 条腿在步相 {stridePhase:F4} 发生足序反转。");
        }
    }

    private static SKPoint UnitVector(SKPoint start, SKPoint end)
    {
        var x = end.X - start.X;
        var y = end.Y - start.Y;
        var length = MathF.Sqrt(x * x + y * y);
        return new SKPoint(x / length, y / length);
    }

    private static float Distance(SKPoint first, SKPoint second)
    {
        var x = second.X - first.X;
        var y = second.Y - first.Y;
        return MathF.Sqrt(x * x + y * y);
    }
}
