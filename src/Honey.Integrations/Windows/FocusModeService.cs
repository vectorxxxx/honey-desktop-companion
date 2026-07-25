using System.Runtime.InteropServices;
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

public sealed class FocusModeService : IDisposable
{
    private readonly HashSet<IntPtr> _ownWindows = [];
    private readonly Timer _timer;
    private bool _disposed;
    private volatile bool _sessionLocked;

    public FocusModeService(TimeSpan? pollInterval = null)
    {
        _timer = new Timer(
            _ => PollSafely(),
            null,
            pollInterval ?? TimeSpan.FromSeconds(1),
            pollInterval ?? TimeSpan.FromSeconds(1));
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

    private void PollSafely()
    {
        try
        {
            var foreground = GetForegroundWindow();
            var shell = GetShellWindow();
            var own = false;
            lock (_ownWindows)
            {
                own = _ownWindows.Contains(foreground);
            }

            var fullscreen = TryGetWindowRect(foreground, out var window)
                && MonitorFromWindow(foreground, MonitorDefaultToNearest) is var monitor
                && monitor != IntPtr.Zero
                && TryGetMonitorBounds(monitor, out var bounds)
                && IsFullscreen(window, bounds);
            var next = new FocusSnapshot(fullscreen, _sessionLocked, own, foreground == shell);
            if (next != Snapshot)
            {
                Snapshot = next;
                Changed?.Invoke(this, next);
            }
        }
        catch
        {
            // Win32 查询失败时安全退化为非专注状态。
            Snapshot = default;
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
