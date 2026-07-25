using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Honey.Integrations.Windows;

namespace Honey.Desktop;

public partial class OverlayWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const int HtClient = 1;
    private const int SafeMarginPixels = 24;

    private readonly DisplayBoundsService _displayBounds;
    private readonly OverlayHitTestPolicy _hitTestPolicy;
    private bool _allowClose;

    public OverlayWindow(
        DisplayBoundsService displayBounds,
        OverlayHitTestPolicy hitTestPolicy)
    {
        _displayBounds = displayBounds;
        _hitTestPolicy = hitTestPolicy;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    public OverlayHitTestPolicy HitTestPolicy => _hitTestPolicy;

    public void PlaceAtPrimaryBottomRight()
    {
        var workArea = _displayBounds.GetWorkAreas().FirstOrDefault();
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            workArea = _displayBounds.GetVirtualScreenBounds();
        }

        var dpi = GetDpi();
        var widthPixels = Math.Max(1, (int)Math.Round(Width * dpi.DpiScaleX));
        var heightPixels = Math.Max(1, (int)Math.Round(Height * dpi.DpiScaleY));
        var position = new PixelRect(
            workArea.Right - widthPixels - SafeMarginPixels,
            workArea.Bottom - heightPixels - SafeMarginPixels,
            widthPixels,
            heightPixels);
        ApplyPhysicalBounds(DisplayBoundsService.ClampWindow(position, workArea), dpi);
    }

    public void RestoreToVisibleWorkArea()
    {
        var dpi = GetDpi();
        var current = new PixelRect(
            (int)Math.Round(Left * dpi.DpiScaleX),
            (int)Math.Round(Top * dpi.DpiScaleY),
            Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY)));
        ApplyPhysicalBounds(_displayBounds.RestoreToVisibleWorkArea(current), dpi);
    }

    public void ShowAndActivate()
    {
        RestoreToVisibleWorkArea();
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void ApplyPhysicalBounds(PixelRect bounds, DpiScale dpi)
    {
        Left = bounds.X / dpi.DpiScaleX;
        Top = bounds.Y / dpi.DpiScaleY;
    }

    private DpiScale GetDpi() =>
        VisualTreeHelper.GetDpi(this) is { DpiScaleX: > 0, DpiScaleY: > 0 } dpi
            ? dpi
            : new DpiScale(1, 1);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProcedure);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private IntPtr WindowProcedure(
        IntPtr windowHandle,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter,
        ref bool handled)
    {
        if (message != WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        if (!GetWindowRect(windowHandle, out var window))
        {
            return IntPtr.Zero;
        }

        var packed = longParameter.ToInt64();
        var screenX = unchecked((short)(packed & 0xffff));
        var screenY = unchecked((short)((packed >> 16) & 0xffff));
        var localPoint = new PixelPoint(screenX - window.Left, screenY - window.Top);
        handled = true;
        return _hitTestPolicy.Resolve(localPoint) == OverlayHitTestResult.Client
            ? new IntPtr(HtClient)
            : new IntPtr(HtTransparent);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
