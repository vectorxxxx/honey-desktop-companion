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
        (IsFullscreen || IsSessionLocked) && !IsOwnWindow && !IsShellWindow;
}

public interface IFocusSnapshotProbe
{
    FocusSnapshot Capture(IReadOnlyCollection<IntPtr> ownWindows);
}

public sealed class FocusModeService : IDisposable
{
    private readonly HashSet<IntPtr> _ownWindows = [];
    private readonly Timer _timer;
    private readonly IFocusSnapshotProbe _probe;
    private bool _disposed;
    private volatile bool _sessionLocked;

    public FocusModeService(
        IFocusSnapshotProbe? probe = null,
        TimeSpan? pollInterval = null)
    {
        _probe = probe ?? new NativeFocusSnapshotProbe(() => _sessionLocked);
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        _timer = new Timer(
            _ => PollNow(),
            null,
            interval,
            interval);
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public FocusSnapshot Snapshot { get; private set; }
    public bool IsFocusModeActive => Snapshot.IsFocusModeActive;
    public event EventHandler<FocusSnapshot>? Changed;

    public void RegisterOwnWindow(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            lock (_ownWindows)
            {
                _ownWindows.Add(handle);
            }
        }
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
        try
        {
            IntPtr[] ownWindows;
            lock (_ownWindows)
            {
                ownWindows = [.. _ownWindows];
            }

            UpdateSnapshot(_probe.Capture(ownWindows));
        }
        catch (Exception exception)
        {
            Trace.TraceError("专注模式前台窗口查询失败：{0}", exception);
            UpdateSnapshot(default);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _timer.Dispose();
    }

    private void UpdateSnapshot(FocusSnapshot next)
    {
        if (next != Snapshot)
        {
            Snapshot = next;
            Changed?.Invoke(this, next);
        }
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

}

internal sealed class NativeFocusSnapshotProbe(
    Func<bool> sessionLocked) : IFocusSnapshotProbe
{
    public FocusSnapshot Capture(IReadOnlyCollection<IntPtr> ownWindows)
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
            sessionLocked(),
            ownWindows.Contains(foreground),
            foreground == shell);
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
