using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;

namespace Honey.Integrations.Windows;

public readonly record struct WindowBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public readonly record struct FocusSnapshot(
    bool IsFullscreen,
    bool IsSessionLocked,
    bool IsOwnWindow,
    bool IsShellWindow)
{
    public bool IsFocusModeActive =>
        IsSessionLocked || (IsFullscreen && !IsOwnWindow && !IsShellWindow);
}

public interface IFocusSnapshotProbe
{
    FocusSnapshot Capture(IReadOnlyCollection<IntPtr> ownWindows);
}

public static class FocusProbePolicy
{
    public static FocusSnapshot CaptureLockedFirst(
        bool isSessionLocked,
        Func<FocusSnapshot> captureForeground)
    {
        ArgumentNullException.ThrowIfNull(captureForeground);
        return isSessionLocked
            ? new FocusSnapshot(false, true, false, false)
            : captureForeground();
    }
}

public sealed class FocusModeService : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<IntPtr, int> _ownWindows = [];
    private readonly IFocusSnapshotProbe _probe;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly Task _pollLoop;
    private readonly object _pollSync = new();
    private bool _disposed;
    private volatile bool _sessionLocked;
    private int _stopped;

    public FocusModeService(
        IFocusSnapshotProbe? probe = null,
        TimeSpan? pollInterval = null)
    {
        _probe = probe ?? new NativeFocusSnapshotProbe(() => _sessionLocked);
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _pollLoop = interval == Timeout.InfiniteTimeSpan
            ? Task.CompletedTask
            : RunPollLoopAsync(interval, _stopSource.Token);
    }

    public FocusSnapshot Snapshot { get; private set; }
    public bool IsFocusModeActive => Snapshot.IsFocusModeActive;
    public event EventHandler<FocusSnapshot>? Changed;
    public event Action<Exception>? Error;

    public IDisposable RegisterOwnWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return EmptyLease.Instance;
        }

        lock (_ownWindows)
        {
            _ownWindows[handle] = _ownWindows.GetValueOrDefault(handle) + 1;
        }

        return new WindowRegistration(this, handle);
    }

    public static bool IsFullscreen(WindowBounds window, WindowBounds monitor, int tolerance = 2) =>
        window.Width > 0
        && window.Height > 0
        && Math.Abs(window.X - monitor.X) <= tolerance
        && Math.Abs(window.Y - monitor.Y) <= tolerance
        && Math.Abs(window.Right - monitor.Right) <= tolerance
        && Math.Abs(window.Bottom - monitor.Bottom) <= tolerance;

    public static bool Evaluate(bool fullscreenOrLocked, bool ownWindow, bool shellWindow) =>
        fullscreenOrLocked && !ownWindow && !shellWindow;

    public void PollNow()
    {
        if (Volatile.Read(ref _stopped) != 0)
        {
            return;
        }

        lock (_pollSync)
        {
            try
            {
                IntPtr[] ownWindows;
                lock (_ownWindows)
                {
                    ownWindows = [.. _ownWindows.Keys];
                }

                UpdateSnapshot(_probe.Capture(ownWindows));
            }
            catch (Exception exception) when (!IsFatal(exception))
            {
                ReportError(exception);
                UpdateSnapshot(default);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAsync().AsTask().GetAwaiter().GetResult();
        _stopSource.Dispose();
    }

    private void UpdateSnapshot(FocusSnapshot next)
    {
        if (next != Snapshot)
        {
            Snapshot = next;
            PublishChanged(next);
        }
    }

    public async ValueTask StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _stopSource.Cancel();
        }

        try
        {
            await _pollLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        _stopSource.Dispose();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
    {
        if (args.Reason == SessionSwitchReason.SessionLock)
        {
            _sessionLocked = true;
        }
        else if (args.Reason == SessionSwitchReason.SessionUnlock)
        {
            _sessionLocked = false;
        }
    }

    private async Task RunPollLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                PollNow();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void PublishChanged(FocusSnapshot snapshot)
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList()
                     .Cast<EventHandler<FocusSnapshot>>())
        {
            try
            {
                subscriber(this, snapshot);
            }
            catch (Exception exception) when (!IsFatal(exception))
            {
                ReportError(exception);
            }
        }
    }

    private void ReportError(Exception exception)
    {
        Trace.TraceError("专注模式轮询或订阅者失败：{0}", exception);
        var handlers = Error;
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList().Cast<Action<Exception>>())
        {
            try
            {
                subscriber(exception);
            }
            catch (Exception sinkException) when (!IsFatal(sinkException))
            {
                Trace.TraceError("专注模式错误观察者失败：{0}", sinkException);
            }
        }
    }

    private void Unregister(IntPtr handle)
    {
        lock (_ownWindows)
        {
            if (!_ownWindows.TryGetValue(handle, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _ownWindows.Remove(handle);
            }
            else
            {
                _ownWindows[handle] = count - 1;
            }
        }
    }

    private static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException;

    private sealed class WindowRegistration(FocusModeService owner, IntPtr handle) : IDisposable
    {
        private FocusModeService? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unregister(handle);
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();
        public void Dispose() { }
    }

}

internal sealed class NativeFocusSnapshotProbe(
    Func<bool> sessionLocked) : IFocusSnapshotProbe
{
    public FocusSnapshot Capture(IReadOnlyCollection<IntPtr> ownWindows)
    {
        return FocusProbePolicy.CaptureLockedFirst(sessionLocked(), () =>
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                throw new InvalidOperationException("无法获取前台窗口。");
            }

            var shell = GetShellWindow();
            var monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
            if (!TryGetWindowRect(foreground, out var window)
                || monitor == IntPtr.Zero
                || !TryGetMonitorBounds(monitor, out var bounds))
            {
                throw new InvalidOperationException("无法读取前台窗口或显示器边界。");
            }

            return new FocusSnapshot(
                FocusModeService.IsFullscreen(window, bounds),
                false,
                ownWindows.Contains(foreground),
                foreground == shell);
        });
    }

    private static bool TryGetWindowRect(IntPtr handle, out WindowBounds bounds)
    {
        bounds = default;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rectangle))
        {
            return false;
        }

        bounds = new WindowBounds(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right - rectangle.Left,
            rectangle.Bottom - rectangle.Top);
        return true;
    }

    private static bool TryGetMonitorBounds(IntPtr monitor, out WindowBounds bounds)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            bounds = default;
            return false;
        }

        bounds = new WindowBounds(
            info.Monitor.Left,
            info.Monitor.Top,
            info.Monitor.Right - info.Monitor.Left,
            info.Monitor.Bottom - info.Monitor.Top);
        return true;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
