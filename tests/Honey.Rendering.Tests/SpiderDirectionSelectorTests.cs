using Honey.Rendering.Spider;

namespace Honey.Rendering.Tests;

public sealed class SpiderDirectionSelectorTests
{
    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 4)]
    [InlineData(0, 1, 8)]
    [InlineData(-1, 0, 12)]
    public void 朝向会映射到十六方向(float x, float y, int expected)
    {
        var selector = new SpiderDirectionSelector();

        var direction = selector.Select(x, y);

        Assert.Equal(expected, direction.Index);
    }

    [Fact]
    public void 边界内小幅摆动会保持当前方向()
    {
        var selector = new SpiderDirectionSelector(hysteresisDegrees: 4);
        var first = selector.Select(0, -1);
        var nearBoundary = selector.Select(
            MathF.Sin(10.8f * MathF.PI / 180),
            -MathF.Cos(10.8f * MathF.PI / 180));

        Assert.Equal(first, nearBoundary);
    }

    [Fact]
    public void 非有限或零向量会保持上次有效方向()
    {
        var selector = new SpiderDirectionSelector();
        var right = selector.Select(1, 0);

        Assert.Equal(right, selector.Select(float.NaN, 0));
        Assert.Equal(right, selector.Select(0, 0));
    }
}
