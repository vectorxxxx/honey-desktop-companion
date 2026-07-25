using Honey.Integrations.Windows;

namespace Honey.Integrations.Tests;

public sealed class FocusModeServiceTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, 0, 0, 1920, 1080, true)]
    [InlineData(1, 1, 1918, 1078, 0, 0, 1920, 1080, true)]
    [InlineData(100, 100, 1200, 800, 0, 0, 1920, 1080, false)]
    public void IsFullscreen_允许两像素误差(
        int x, int y, int width, int height,
        int workX, int workY, int workWidth, int workHeight,
        bool expected)
    {
        Assert.Equal(
            expected,
            FocusModeService.IsFullscreen(
                new WindowBounds(x, y, width, height),
                new WindowBounds(workX, workY, workWidth, workHeight)));
    }

    [Fact]
    public void Evaluate_忽略自身与桌面Shell()
    {
        Assert.False(FocusModeService.Evaluate(true, true, false));
        Assert.False(FocusModeService.Evaluate(true, false, true));
        Assert.True(FocusModeService.Evaluate(true, false, false));
    }

    [Fact]
    public void FocusSnapshot_锁屏会激活专注而自身窗口不会()
    {
        Assert.True(new FocusSnapshot(false, true, false, false).IsFocusModeActive);
        Assert.False(new FocusSnapshot(false, true, true, false).IsFocusModeActive);
    }

    [Fact]
    public void PollNow_活动状态后查询失败会通知一次安全降级()
    {
        var probe = new SequenceFocusProbe(
            new FocusSnapshot(true, false, false, false),
            new InvalidOperationException("Win32失败"),
            new InvalidOperationException("仍失败"));
        using var service = new FocusModeService(probe, Timeout.InfiniteTimeSpan);
        var changes = new List<bool>();
        service.Changed += (_, snapshot) => changes.Add(snapshot.IsFocusModeActive);

        service.PollNow();
        service.PollNow();
        service.PollNow();

        Assert.Equal([true, false], changes);
        Assert.False(service.IsFocusModeActive);
    }

    [Fact]
    public void PollNow_初始非活动查询失败不会产生多余通知()
    {
        using var service = new FocusModeService(
            new SequenceFocusProbe(new InvalidOperationException("失败")),
            Timeout.InfiniteTimeSpan);
        var count = 0;
        service.Changed += (_, _) => count++;

        service.PollNow();

        Assert.Equal(0, count);
    }

    private sealed class SequenceFocusProbe(params object[] results) : IFocusSnapshotProbe
    {
        private readonly Queue<object> _results = new(results);

        public FocusSnapshot Capture(IReadOnlyCollection<nint> ownWindows)
        {
            var result = _results.Dequeue();
            return result is Exception exception
                ? throw exception
                : (FocusSnapshot)result;
        }
    }
}
