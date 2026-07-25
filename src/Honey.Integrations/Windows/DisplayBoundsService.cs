using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Honey.Integrations.Windows;

/// <summary>
/// 以 Win32 物理像素表达的位置和尺寸。
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);
}

/// <summary>
/// 提供显示器工作区。该边界均为 Win32 物理像素；WPF 调用方负责在边界处与 DIP 换算。
/// </summary>
public sealed class DisplayBoundsService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public PixelRect GetVirtualScreenBounds() => new(
        GetSystemMetrics(SmXVirtualScreen),
        GetSystemMetrics(SmYVirtualScreen),
        GetSystemMetrics(SmCxVirtualScreen),
        GetSystemMetrics(SmCyVirtualScreen));

    public IReadOnlyList<PixelRect> GetWorkAreas()
    {
        var workAreas = new List<PixelRect>();
        var callbackError = 0;
        MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                callbackError = Marshal.GetLastWin32Error();
                return false;
            }

            workAreas.Add(ToPixelRect(info.WorkArea));
            return true;
        };

        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new Win32Exception(
                callbackError == 0 ? Marshal.GetLastWin32Error() : callbackError);
        }

        GC.KeepAlive(callback);
        return workAreas;
    }

    public PixelRect RestoreToVisibleWorkArea(PixelRect window)
    {
        var workAreas = GetWorkAreas();
        if (workAreas.Count == 0)
        {
            return ClampWindow(window, GetVirtualScreenBounds());
        }

        return ClampWindow(window, FindNearestWorkArea(window, workAreas));
    }

    public static PixelRect ClampWindow(PixelRect window, PixelRect workArea)
    {
        Validate(window, nameof(window));
        Validate(workArea, nameof(workArea));

        var maxX = workArea.Width >= window.Width ? workArea.Right - window.Width : workArea.X;
        var maxY = workArea.Height >= window.Height ? workArea.Bottom - window.Height : workArea.Y;
        var x = Math.Clamp(window.X, workArea.X, maxX);
        var y = Math.Clamp(window.Y, workArea.Y, maxY);
        return window with { X = x, Y = y };
    }

    public static PixelRect FindNearestWorkArea(
        PixelRect window,
        IReadOnlyCollection<PixelRect> workAreas)
    {
        Validate(window, nameof(window));
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
        {
            throw new ArgumentException("至少需要一个显示器工作区。", nameof(workAreas));
        }

        return workAreas
            .Select(area => (Area: area, Intersection: IntersectionArea(window, area), Distance: CenterDistance(window, area)))
            .OrderByDescending(candidate => candidate.Intersection)
            .ThenBy(candidate => candidate.Distance)
            .First()
            .Area;
    }

    private static long IntersectionArea(PixelRect left, PixelRect right)
    {
        var width = Math.Max(0, Math.Min(left.Right, right.Right) - Math.Max(left.X, right.X));
        var height = Math.Max(0, Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Y, right.Y));
        return (long)width * height;
    }

    private static double CenterDistance(PixelRect left, PixelRect right)
    {
        var deltaX = left.X + left.Width / 2d - (right.X + right.Width / 2d);
        var deltaY = left.Y + left.Height / 2d - (right.Y + right.Height / 2d);
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static void Validate(PixelRect rectangle, string parameterName)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "矩形尺寸必须大于零。");
        }
    }

    private static PixelRect ToPixelRect(NativeRect rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);

    private delegate bool MonitorEnumProc(
        IntPtr monitor,
        IntPtr deviceContext,
        IntPtr monitorRectangle,
        IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRectangle,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
