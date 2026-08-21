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
    public void FocusSnapshot_全屏时忽略自身与桌面Shell()
    {
        Assert.False(new FocusSnapshot(true, false, true, false).IsFocusModeActive);
        Assert.False(new FocusSnapshot(true, false, false, true).IsFocusModeActive);
        Assert.True(new FocusSnapshot(true, false, false, false).IsFocusModeActive);
    }

    [Fact]
    public void FocusSnapshot_锁屏会激活专注而自身窗口不会()
    {
        Assert.True(new FocusSnapshot(false, true, false, false).IsFocusModeActive);
        Assert.True(new FocusSnapshot(false, true, true, false).IsFocusModeActive);
        Assert.True(new FocusSnapshot(false, true, false, true).IsFocusModeActive);
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

    [Fact]
    public void CaptureLockedFirst_锁屏时不调用会失败的前台探针()
    {
        var calls = 0;
        var snapshot = FocusProbePolicy.CaptureLockedFirst(
            true,
            () =>
            {
                calls++;
                throw new InvalidOperationException("前台查询失败");
            });

        Assert.True(snapshot.IsFocusModeActive);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task PollLoop_订阅者失败不阻断后续且停止后不再回调()
    {
        var probe = new ConstantFocusProbe(new FocusSnapshot(true, false, false, false));
        await using var service = new FocusModeService(probe, TimeSpan.FromMilliseconds(10));
        var received = 0;
        service.Changed += (_, _) => throw new InvalidOperationException("订阅失败");
        service.Changed += (_, _) => Interlocked.Increment(ref received);

        await probe.WaitForSecondCaptureAsync(TestContext.Current.CancellationToken);
        await service.StopAsync();
        var stoppedAt = Volatile.Read(ref received);
        await Task.Delay(40, TestContext.Current.CancellationToken);

        Assert.Equal(1, stoppedAt);
        Assert.Equal(stoppedAt, Volatile.Read(ref received));
        Assert.True(probe.CaptureCount > 1);
    }

    [Fact]
    public void RegisterOwnWindow_释放租约后解除注册()
    {
        var probe = new RecordingFocusProbe();
        using var service = new FocusModeService(probe, Timeout.InfiniteTimeSpan);
        var lease = service.RegisterOwnWindow(new IntPtr(42));
        service.PollNow();
        Assert.Contains(new IntPtr(42), probe.LastOwnWindows);

        lease.Dispose();
        service.PollNow();
        Assert.DoesNotContain(new IntPtr(42), probe.LastOwnWindows);
    }

    private sealed class ConstantFocusProbe(FocusSnapshot snapshot) : IFocusSnapshotProbe
    {
        private readonly TaskCompletionSource _secondCapture = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _captureCount;

        public int CaptureCount => Volatile.Read(ref _captureCount);

        public FocusSnapshot Capture(IReadOnlyCollection<nint> ownWindows)
        {
            if (Interlocked.Increment(ref _captureCount) >= 2)
            {
                _secondCapture.TrySetResult();
            }

            return snapshot;
        }

        public Task WaitForSecondCaptureAsync(CancellationToken cancellationToken) =>
            _secondCapture.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    private sealed class RecordingFocusProbe : IFocusSnapshotProbe
    {
        public IReadOnlyCollection<nint> LastOwnWindows { get; private set; } = [];
        public FocusSnapshot Capture(IReadOnlyCollection<nint> ownWindows)
        {
            LastOwnWindows = ownWindows.ToArray();
            return default;
        }
    }
}
