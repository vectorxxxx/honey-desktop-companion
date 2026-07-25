using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Honey.Integrations.Windows;

public readonly record struct PixelPoint(int X, int Y);

public sealed class CursorService
{
    public PixelPoint GetPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PixelPoint(point.X, point.Y);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
