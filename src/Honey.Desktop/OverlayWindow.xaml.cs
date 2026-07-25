using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Interaction;
using Honey.Domain.Events;
using Honey.Domain.Model;
using Honey.Integrations.Windows;
using Honey.Rendering;
using Honey.Rendering.Spider;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Honey.Desktop;

public partial class OverlayWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const int HtClient = 1;
    private const int SafeMarginPixels = 24;
    private readonly DisplayBoundsService _displayBounds;
    private readonly OverlayHitTestPolicy _hitTestPolicy;
    private readonly WhiteJadeSpiderScene _scene = new();
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();
    private readonly PetInteractionController _interactionController;
    private readonly Guid _petId;
    private RenderSnapshot _snapshot;
    private SpiderHitMap _hitMap = SpiderHitMap.CreateDefault(0, 0, 1);
    private float _canvasCoordinateWidth;
    private float _canvasCoordinateHeight;
    private bool _menuOpen;
    private bool _paused;
    private double _frozenAnimationTime;
    private bool _allowClose;

    public OverlayWindow(
        DisplayBoundsService displayBounds,
        OverlayHitTestPolicy hitTestPolicy)
    {
        _displayBounds = displayBounds;
        _hitTestPolicy = hitTestPolicy;
        var initial = new WhiteJadeSpiderPack().CreateInitialState(DateTimeOffset.UtcNow);
        _petId = initial.PetId;
        _snapshot = new RenderSnapshot(
            initial.Mode,
            initial.Mood,
            0,
            0,
            0,
            (float)initial.Scale,
            "observe").Normalize();
        _interactionController = new PetInteractionController(
            initial.PetId,
            interaction => InteractionOccurred?.Invoke(interaction),
            MoveWindowPhysical,
            paused => AutonomousMovementPaused?.Invoke(paused));

        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        Closed += OnClosed;
        Loaded += OnLoaded;
        IsVisibleChanged += OnVisibilityChanged;
        StateChanged += OnWindowStateChanged;
        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonUp += OnMouseRightButtonUp;
        LostMouseCapture += OnLostMouseCapture;
        PreviewKeyDown += OnPreviewKeyDown;
        _hitTestPolicy.Update(HitTestPhysicalPoint);
    }

    public event Action<PetInteractionOccurred>? InteractionOccurred;

    public event Action<bool>? AutonomousMovementPaused;

    public OverlayHitTestPolicy HitTestPolicy => _hitTestPolicy;

    public RenderSnapshot Snapshot
    {
        get => Volatile.Read(ref _snapshot);
        set => Volatile.Write(ref _snapshot, (value ?? throw new ArgumentNullException(nameof(value))).Normalize());
    }

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

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        _canvasCoordinateWidth = e.Info.Width;
        _canvasCoordinateHeight = e.Info.Height;
        var current = Snapshot with
        {
            AnimationTime = _paused ? _frozenAnimationTime : _animationClock.Elapsed.TotalSeconds
        };
        _hitMap = SpiderHitMap.CreateForSnapshot(
            _canvasCoordinateWidth,
            _canvasCoordinateHeight,
            current);
        _scene.Draw(canvas, current, _canvasCoordinateWidth, _canvasCoordinateHeight);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        SpiderCanvas.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateRenderingSubscription();
        SpiderCanvas.InvalidateVisual();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        UpdateRenderingSubscription();

    private void OnWindowStateChanged(object? sender, EventArgs e) =>
        UpdateRenderingSubscription();

    private void UpdateRenderingSubscription()
    {
        CompositionTarget.Rendering -= OnRendering;
        if (IsVisible && WindowState != WindowState.Minimized && !_paused)
        {
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _interactionController.Cancel();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var point = e.GetPosition(SpiderCanvas);
        var lookX = SpiderCanvas.ActualWidth <= 0
            ? 0
            : (float)Math.Clamp((point.X / SpiderCanvas.ActualWidth - 0.5) * 2, -1, 1);
        var lookY = SpiderCanvas.ActualHeight <= 0
            ? 0
            : (float)Math.Clamp((point.Y / SpiderCanvas.ActualHeight - 0.5) * 2, -1, 1);
        Snapshot = Snapshot with { LookX = lookX, LookY = lookY };

        if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
        {
            var screen = PointToScreen(e.GetPosition(this));
            _interactionController.Move(new PixelPoint((int)Math.Round(screen.X), (int)Math.Round(screen.Y)));
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_menuOpen)
        {
            return;
        }

        var screen = PointToScreen(e.GetPosition(this));
        var origin = GetPhysicalWindowOrigin();
        _interactionController.Begin(
            new PixelPoint((int)Math.Round(screen.X), (int)Math.Round(screen.Y)),
            origin);
        Mouse.Capture(this);
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured)
        {
            return;
        }

        var screen = PointToScreen(e.GetPosition(this));
        _interactionController.End(new PixelPoint((int)Math.Round(screen.X), (int)Math.Round(screen.Y)));
        Mouse.Capture(null);
        if (!_interactionController.IsDragging)
        {
            RestoreToVisibleWorkArea();
        }

        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) =>
        _interactionController.Cancel();

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        SetMenuOpen(true);
        e.Handled = true;
    }

    private void OnPetButtonClick(object sender, RoutedEventArgs e)
    {
        Snapshot = Snapshot with { Mood = PetMood.Happy };
        InteractionOccurred?.Invoke(new PetInteractionOccurred(_petId, "pet"));
        SetMenuOpen(false);
    }

    private void OnPauseButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_paused)
        {
            _frozenAnimationTime = _animationClock.Elapsed.TotalSeconds;
        }

        _paused = !_paused;
        AutonomousMovementPaused?.Invoke(_paused);
        UpdateRenderingSubscription();
        if (!_paused)
        {
            SpiderCanvas.InvalidateVisual();
        }

        SetMenuOpen(false);
    }

    private void OnSleepButtonClick(object sender, RoutedEventArgs e)
    {
        Snapshot = Snapshot with { Mood = PetMood.Sleepy, Behavior = "sleep" };
        SetMenuOpen(false);
    }

    private void OnModeButtonClick(object sender, RoutedEventArgs e)
    {
        Snapshot = Snapshot with
        {
            Mode = Snapshot.Mode == PetMode.Normal ? PetMode.Berserk : PetMode.Normal,
            Mood = Snapshot.Mode == PetMode.Normal ? PetMood.Angry : PetMood.Curious
        };
        SpiderCanvas.InvalidateVisual();
        SetMenuOpen(false);
    }

    private void OnCloseMenuButtonClick(object sender, RoutedEventArgs e) =>
        SetMenuOpen(false);

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_menuOpen && e.Key == Key.Escape)
        {
            SetMenuOpen(false);
            e.Handled = true;
        }
    }

    private void SetMenuOpen(bool open)
    {
        _menuOpen = open;
        RadialMenu.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open)
        {
            PetMenuButton.Focus();
        }
    }

    private bool HitTestPhysicalPoint(PixelPoint localPhysical)
    {
        if (_menuOpen)
        {
            var dpi = GetDpi();
            var centerX = ActualWidth * dpi.DpiScaleX / 2;
            var centerY = ActualHeight * dpi.DpiScaleY / 2;
            var deltaX = localPhysical.X - centerX;
            var deltaY = localPhysical.Y - centerY;
            if (deltaX * deltaX + deltaY * deltaY <= Math.Pow(140 * dpi.DpiScaleX, 2))
            {
                return true;
            }
        }

        var windowSize = GetPhysicalWindowSize();
        var windowPixelsWidth = Math.Max(1, windowSize.Width);
        var windowPixelsHeight = Math.Max(1, windowSize.Height);
        var canvasX = localPhysical.X * _canvasCoordinateWidth / windowPixelsWidth;
        var canvasY = localPhysical.Y * _canvasCoordinateHeight / windowPixelsHeight;
        return _hitMap.Contains((float)canvasX, (float)canvasY);
    }

    private void MoveWindowPhysical(PixelPoint position)
    {
        var dpi = GetDpi();
        Left = position.X / dpi.DpiScaleX;
        Top = position.Y / dpi.DpiScaleY;
    }

    private PixelPoint GetPhysicalWindowOrigin()
    {
        var handle = new WindowInteropHelper(this).Handle;
        return handle != IntPtr.Zero && GetWindowRect(handle, out var rectangle)
            ? new PixelPoint(rectangle.Left, rectangle.Top)
            : new PixelPoint(
                (int)Math.Round(Left * GetDpi().DpiScaleX),
                (int)Math.Round(Top * GetDpi().DpiScaleY));
    }

    private (int Width, int Height) GetPhysicalWindowSize()
    {
        var handle = new WindowInteropHelper(this).Handle;
        return handle != IntPtr.Zero && GetWindowRect(handle, out var rectangle)
            ? (Math.Max(0, rectangle.Right - rectangle.Left), Math.Max(0, rectangle.Bottom - rectangle.Top))
            : (0, 0);
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
