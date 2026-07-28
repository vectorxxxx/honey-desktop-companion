using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Rendering.Spider;
using SkiaSharp;

namespace Honey.Rendering.Tests;

public sealed class SpiderNaturalSilhouetteTests
{
    [Theory]
    [MemberData(nameof(全尺寸像素))]
    public void Create_各尺寸每条腿均有自然髋膝折角(float petPixels)
    {
        var layout = SpiderLayout.Create(320, 320, petPixels / 140f);

        for (var legIndex = 0; legIndex < layout.Legs.Count; legIndex++)
        {
            var leg = layout.Legs[legIndex];
            var hipTurn = TurnDegrees(leg.Root, leg.Hip, leg.Knee);
            var kneeTurn = TurnDegrees(leg.Hip, leg.Knee, leg.Tip);
            var majorTurn = Math.Max(hipTurn, kneeTurn);

            Assert.True(
                majorTurn is >= 22f and <= 85f,
                $"{petPixels}px 第 {legIndex} 条腿静态主折角 {majorTurn:F3}° 超界，"
                + $"髋角 {hipTurn:F3}°，膝角 {kneeTurn:F3}°。");
        }
    }

    [Fact]
    public void Create_最小尺寸增强折膝且在标准尺寸边界连续收敛()
    {
        var at60 = SpiderLayout.Create(320, 320, 60f / 140f);
        var at90 = SpiderLayout.Create(320, 320, 90f / 140f);
        var below90 = SpiderLayout.Create(320, 320, 89.9f / 140f);
        var above90 = SpiderLayout.Create(320, 320, 90.1f / 140f);
        var movablePointDistances = new List<float>();

        for (var legIndex = 0; legIndex < at60.Legs.Count; legIndex++)
        {
            var at60Leg = at60.Legs[legIndex];
            var at90Leg = at90.Legs[legIndex];
            var below90Leg = below90.Legs[legIndex];
            var above90Leg = above90.Legs[legIndex];
            var joints = new[]
            {
                ("Root", at60Leg.Root, at90Leg.Root, below90Leg.Root, above90Leg.Root),
                ("Hip", at60Leg.Hip, at90Leg.Hip, below90Leg.Hip, above90Leg.Hip),
                ("Knee", at60Leg.Knee, at90Leg.Knee, below90Leg.Knee, above90Leg.Knee),
                ("Tip", at60Leg.Tip, at90Leg.Tip, below90Leg.Tip, above90Leg.Tip)
            };

            foreach (var joint in joints)
            {
                var boundaryDistance = Distance(
                    Normalize(below90, joint.Item4),
                    Normalize(above90, joint.Item5));
                Assert.True(
                    boundaryDistance <= 0.01f,
                    $"第 {legIndex} 条腿的 {joint.Item1} 在 89.9/90.1px 边界不连续：{boundaryDistance:F4}。");
            }

            var rootDistance = Distance(
                Normalize(at60, at60Leg.Root),
                Normalize(at90, at90Leg.Root));
            Assert.True(
                rootDistance <= 0.0005f,
                $"第 {legIndex} 条腿的 Root 在 60/90px 间发生位移：{rootDistance:F4}。");
            movablePointDistances.Add(Distance(
                Normalize(at60, at60Leg.Hip),
                Normalize(at90, at90Leg.Hip)));
            movablePointDistances.Add(Distance(
                Normalize(at60, at60Leg.Knee),
                Normalize(at90, at90Leg.Knee)));
            movablePointDistances.Add(Distance(
                Normalize(at60, at60Leg.Tip),
                Normalize(at90, at90Leg.Tip)));
        }

        var maximumMovablePointDistance = movablePointDistances.Max();
        Assert.True(
            maximumMovablePointDistance >= 0.04f,
            $"60/90px 全部可动骨点的最大归一化距离仅为 {maximumMovablePointDistance:F4}。");
    }

    [Fact]
    public void Create_六十至九十像素逐像素平滑插值全部关节()
    {
        const float maximumStepDistance = 0.02f;
        for (var petPixels = 60; petPixels < 90; petPixels++)
        {
            var current = SpiderLayout.Create(320, 320, petPixels / 140f);
            var next = SpiderLayout.Create(320, 320, (petPixels + 1) / 140f);
            for (var legIndex = 0; legIndex < current.Legs.Count; legIndex++)
            {
                var currentJoints = Joints(current.Legs[legIndex]);
                var nextJoints = Joints(next.Legs[legIndex]);
                for (var jointIndex = 0; jointIndex < currentJoints.Length; jointIndex++)
                {
                    var distance = Distance(
                        Normalize(current, currentJoints[jointIndex].Point),
                        Normalize(next, nextJoints[jointIndex].Point));
                    Assert.True(
                        distance <= maximumStepDistance,
                        $"{petPixels}/{petPixels + 1}px 第 {legIndex} 条腿的"
                        + $"{currentJoints[jointIndex].Name} 插值步长 {distance:F4} 超过"
                        + $" {maximumStepDistance:F2}。");
                }
            }
        }
    }

    [Fact]
    public void CreatePose_全尺寸待机动画始终保持自然折膝()
    {
        AngleSample? minimum = null;
        AngleSample? maximum = null;
        foreach (var petPixels in PetPixelValues)
        {
            for (var sample = 0; sample < 360; sample++)
            {
                var animationTime = sample / 360d * Math.Tau / 3.1d;
                var pose = SpiderGeometry.CreatePose(
                    320,
                    320,
                    new RenderSnapshot(
                        PetMode.Normal,
                        PetMood.Curious,
                        0,
                        0,
                        animationTime,
                        petPixels / 140f,
                        "测试") with
                    {
                        NormalizedSpeed = 0,
                        FacingX = 0,
                        FacingY = -1
                    });

                MeasurePoseAngles(
                    pose,
                    petPixels,
                    animationTime,
                    ref minimum,
                    ref maximum);
            }
        }

        AssertAngleRange(minimum, maximum, 22f, 85f, "待机动画");
    }

    [Fact]
    public void CreatePose_全尺寸最大移动步态保持折角与同侧足序()
    {
        AngleSample? minimum = null;
        AngleSample? maximum = null;
        foreach (var petPixels in PetPixelValues)
        {
            for (var sample = 0; sample < 360; sample++)
            {
                var stridePhase = sample / 360f;
                var pose = SpiderGeometry.CreatePose(
                    320,
                    320,
                    new RenderSnapshot(
                        PetMode.Normal,
                        PetMood.Alert,
                        0,
                        0,
                        0,
                        petPixels / 140f,
                        "测试") with
                    {
                        NormalizedSpeed = 1,
                        StridePhase = stridePhase,
                        FacingX = 0,
                        FacingY = -1
                    });

                MeasurePoseAngles(
                    pose,
                    petPixels,
                    stridePhase,
                    ref minimum,
                    ref maximum);
                AssertSameSideOrder(pose.Legs.Take(4), stridePhase, $"{petPixels}px 左侧");
                AssertSameSideOrder(pose.Legs.Skip(4), stridePhase, $"{petPixels}px 右侧");
            }
        }

        AssertAngleRange(minimum, maximum, 18f, 85f, "最大移动步态");
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
                0,
                0,
                1,
                "测试",
                FacingX: 0,
                FacingY: -1));

        for (var directionIndex = 0; directionIndex < SpiderDirection.Count; directionIndex++)
        {
            var angle = directionIndex * MathF.Tau / SpiderDirection.Count;
            var pose = SpiderGeometry.CreatePose(
                320,
                320,
                new RenderSnapshot(
                    PetMode.Normal,
                    PetMood.Curious,
                    0,
                    0,
                    0,
                    1,
                    "测试",
                    FacingX: MathF.Sin(angle),
                    FacingY: -MathF.Cos(angle)));

            for (var legIndex = 0; legIndex < pose.Legs.Count; legIndex++)
            {
                var expected = up.Legs[legIndex];
                var actual = pose.Legs[legIndex];
                AssertTurnPreserved(
                    TurnDegrees(expected.Root, expected.Hip, expected.Knee),
                    TurnDegrees(actual.Root, actual.Hip, actual.Knee),
                    directionIndex,
                    legIndex,
                    "髋");
                AssertTurnPreserved(
                    TurnDegrees(expected.Hip, expected.Knee, expected.Tip),
                    TurnDegrees(actual.Hip, actual.Knee, actual.Tip),
                    directionIndex,
                    legIndex,
                    "膝");
            }
        }
    }

    public static IEnumerable<object[]> 全尺寸像素 =>
        PetPixelValues.Select(petPixels => new object[] { petPixels });

    private static IReadOnlyList<float> PetPixelValues { get; } =
        Enumerable.Range(60, 31)
            .Select(petPixels => (float)petPixels)
            .Concat([140f, 240f])
            .ToArray();

    private static void MeasurePoseAngles(
        SpiderPose pose,
        float petPixels,
        double phase,
        ref AngleSample? minimum,
        ref AngleSample? maximum)
    {
        for (var legIndex = 0; legIndex < pose.Legs.Count; legIndex++)
        {
            var leg = pose.Legs[legIndex];
            var hipTurn = TurnDegrees(leg.Root, leg.Hip, leg.Knee);
            var kneeTurn = TurnDegrees(leg.Hip, leg.Knee, leg.Tip);
            var sample = new AngleSample(
                petPixels,
                phase,
                legIndex,
                hipTurn,
                kneeTurn);
            if (minimum is null || sample.MajorTurn < minimum.Value.MajorTurn)
            {
                minimum = sample;
            }

            if (maximum is null || sample.MajorTurn > maximum.Value.MajorTurn)
            {
                maximum = sample;
            }
        }
    }

    private static void AssertAngleRange(
        AngleSample? minimum,
        AngleSample? maximum,
        float lower,
        float upper,
        string context)
    {
        Assert.NotNull(minimum);
        Assert.NotNull(maximum);
        Assert.True(
            minimum.Value.MajorTurn >= lower,
            $"{context}最小主折角低于 {lower:F0}°：{Describe(minimum.Value)}");
        Assert.True(
            maximum.Value.MajorTurn <= upper,
            $"{context}最大主折角高于 {upper:F0}°：{Describe(maximum.Value)}");
    }

    private static string Describe(AngleSample sample) =>
        $"{sample.PetPixels}px，相位/时间 {sample.Phase:F6}，第 {sample.LegIndex} 条腿，"
        + $"主折角 {sample.MajorTurn:F3}°，髋角 {sample.HipTurn:F3}°，"
        + $"膝角 {sample.KneeTurn:F3}°。";

    private static void AssertTurnPreserved(
        float expected,
        float actual,
        int directionIndex,
        int legIndex,
        string joint)
    {
        var difference = Math.Abs(expected - actual);
        Assert.True(
            difference <= 0.001f,
            $"方向 {directionIndex} 第 {legIndex} 条腿{joint}角变化 {difference:F6}°："
            + $"期望 {expected:F3}°，实际 {actual:F3}°。");
    }

    private static float TurnDegrees(SKPoint start, SKPoint joint, SKPoint end)
    {
        var first = UnitVector(start, joint);
        var second = UnitVector(joint, end);
        var dot = Math.Clamp(first.X * second.X + first.Y * second.Y, -1f, 1f);
        return MathF.Acos(dot) * 180f / MathF.PI;
    }

    private static SKPoint Normalize(SpiderLayout layout, SKPoint point)
    {
        var unit = layout.Abdomen.Width / 1.52f;
        return new SKPoint(
            (point.X - layout.Center.X) / unit,
            (point.Y - layout.Center.Y) / unit);
    }

    private static (string Name, SKPoint Point)[] Joints(SpiderLeg leg) =>
    [
        ("Root", leg.Root),
        ("Hip", leg.Hip),
        ("Knee", leg.Knee),
        ("Tip", leg.Tip)
    ];

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

    private readonly record struct AngleSample(
        float PetPixels,
        double Phase,
        int LegIndex,
        float HipTurn,
        float KneeTurn)
    {
        public float MajorTurn => Math.Max(HipTurn, KneeTurn);
    }
}
