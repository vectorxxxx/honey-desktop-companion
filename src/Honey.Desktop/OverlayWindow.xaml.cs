using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Interaction;
using Honey.Desktop.Movement;
using Honey.Desktop.Rendering;
using Honey.Desktop.Runtime;
using Honey.Desktop.Settings;
using Honey.Domain.Behavior;
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
    private readonly PausableAnimationClock _animationClock = new();
    private readonly PauseCoordinator _pauseCoordinator;
    private readonly PetInteractionController _interactionController;
    private readonly PointerInteractionFinalizer _interactionFinalizer;
    private readonly Guid _petId;
    private readonly PetRuntimeController _runtime;
    private readonly CursorService _cursorService = new();
    private readonly DesktopLocomotionController _locomotionController;
    private readonly DispatcherTimer _movementTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(33)
    };
    private readonly Stopwatch _movementClock = Stopwatch.StartNew();
    private RenderSnapshot _snapshot;
    private SpiderHitMap _hitMap = SpiderHitMap.CreateDefault(320, 320, 1);
    private PixelRect _contentBoundsPhysical = new(80, 80, 160, 160);
    private float _canvasCoordinateWidth;
    private float _canvasCoordinateHeight;
    private bool _menuOpen;
    private bool _userPaused;
    private bool _allowClose;
    private int _disposed;
    private readonly DispatcherTimer _thoughtTimer = new()
    {
        Interval = TimeSpan.FromSeconds(6)
    };

    public OverlayWindow(
        DisplayBoundsService displayBounds,
        OverlayHitTestPolicy hitTestPolicy,
        PetState? initialState = null,
        AppSettings? settings = null)
    {
        _displayBounds = displayBounds;
        _hitTestPolicy = hitTestPolicy;
        var initial = initialState ?? new WhiteJadeSpiderPack().CreateInitialState(DateTimeOffset.UtcNow);
        var appliedSettings = (settings ?? new AppSettings()).Normalize();
        _petId = initial.PetId;
        _snapshot = new RenderSnapshot(
            initial.Mode,
            initial.Mood,
            0,
            0,
            0,
            (float)(appliedSettings.PetSize / 140d),
            BuiltInBehaviorKeys.Observe).Normalize();
        _runtime = new PetRuntimeController(initial, appliedSettings);
        _runtime.SetHidden(true);
        _runtime.SnapshotChanged += OnRuntimeSnapshotChanged;
        _pauseCoordinator = new PauseCoordinator(
            paused => SafeEventDispatcher.Publish(
                AutonomousMovementPaused,
                paused,
                ReportInputError),
            ReportInputError);
        _interactionController = new PetInteractionController(
            initial.PetId,
            interaction => SafeEventDispatcher.Publish(
                InteractionOccurred,
                interaction,
                ReportInputError),
            MoveWindowPhysical,
            paused => _pauseCoordinator.Set(PauseReason.Drag, paused),
            ReportInputError);
        _interactionFinalizer = new PointerInteractionFinalizer(_interactionController);

        InitializeComponent();
        _locomotionController = new DesktopLocomotionController(
            GetPhysicalWindowOrigin,
            () => _contentBoundsPhysical,
            _displayBounds.GetWorkAreas,
            _cursorService.GetPosition,
            MoveWindowPhysical,
            WhiteJadeSpiderPack.LocomotionProfile);
        _locomotionController.UpdateSettings(
            appliedSettings.AutonomousMovementEnabled,
            appliedSettings.AllowCrossMonitorRoaming);
        _locomotionController.UpdateSnapshot(_snapshot);
        _movementTimer.Tick += OnMovementTimerTick;
        _thoughtTimer.Tick += OnThoughtTimerTick;
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
        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
        _hitTestPolicy.Update(HitTestPhysicalPoint);
    }

    public event Action<PetInteractionOccurred>? InteractionOccurred;

    public event Action<bool>? AutonomousMovementPaused;
    public event Action<bool>? UserPauseChanged;
    public event Action? PetCommandRequested;
    public event Action<BehaviorKey>? SkillCommandRequested;
    public event Action? AiInsightRequested;

    public OverlayHitTestPolicy HitTestPolicy => _hitTestPolicy;
    public PetState RuntimeState => _runtime.State;
    public IPetRuntimeCommands RuntimeCommands => _runtime;
    public IPetRuntimeLifecycle RuntimeLifecycle => _runtime;

    public string CreateAiStateSummary()
    {
        var state = _runtime.State;
        return $"情绪：{MoodName(state.Mood)}；形态：{ModeName(state.Mode)}；"
            + $"行为：{Snapshot.Behavior}；需求："
            + $"饥饿{NeedBand(state.Needs.Hunger)}、精力{NeedBand(state.Needs.Energy)}、"
            + $"好奇{NeedBand(state.Needs.Curiosity)}、亲密{NeedBand(state.Needs.Affection)}、"
            + $"压力{NeedBand(state.Needs.Stress)}";
    }

    public void ShowThought(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => ShowThought(text));
            return;
        }

        ThoughtText.Text = string.IsNullOrWhiteSpace(text)
            ? "小玉歪了歪头，继续自己的探索。"
            : text.Trim()[..Math.Min(text.Trim().Length, 800)];
        ThoughtBubble.Visibility = Visibility.Visible;
        _thoughtTimer.Stop();
        _thoughtTimer.Start();
    }

    public RenderSnapshot Snapshot
    {
        get => Volatile.Read(ref _snapshot);
        set
        {
            var normalized =
                (value ?? throw new ArgumentNullException(nameof(value))).Normalize();
            var previous = Volatile.Read(ref _snapshot);
            Volatile.Write(ref _snapshot, normalized);
            if (Math.Abs(previous.Scale - normalized.Scale) > float.Epsilon)
            {
                ResizeForScale(normalized.Scale);
            }
        }
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
        FinalizePointerInteraction(null, complete: false);
        _allowClose = true;
        Close();
    }

    public void ApplySettings(AppSettings settings)
    {
        var normalized = settings.Normalize();
        _runtime.ApplySettings(normalized);
        _locomotionController.UpdateSettings(
            normalized.AutonomousMovementEnabled,
            normalized.AllowCrossMonitorRoaming);
        Snapshot = Snapshot with
        {
            Scale = normalized.PetSize / 140f,
            Mode = PetRuntimePolicy.ApplyModePreference(normalized.ModePreference, Snapshot.Mode)
        };
    }

    public void SetUserPaused(bool paused)
    {
        if (_userPaused == paused)
        {
            return;
        }

        _userPaused = paused;
        _runtime.SetPaused(paused);
        _pauseCoordinator.Set(PauseReason.User, paused);
        _animationClock.SetPaused(AnimationPauseReason.User, paused);
        UpdatePauseButtonVisual();
        UpdateRenderingSubscription();
        SpiderCanvas.InvalidateVisual();
    }

    public void SetFocusActive(bool active) => _runtime.SetFocusActive(active);

    public void StopRuntime() => _runtime.Dispose();

    public Task StopRuntimeAsync() => _runtime.StopAsync();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        _canvasCoordinateWidth = e.Info.Width;
        _canvasCoordinateHeight = e.Info.Height;
        var dpi = GetDpi();
        var deviceScale = CanvasDensityResolver.Resolve(
            e.Info.Width,
            e.Info.Height,
            SpiderCanvas.ActualWidth,
            SpiderCanvas.ActualHeight,
            dpi.DpiScaleX,
            dpi.DpiScaleY);
        var current = Snapshot;
        var pose = SpiderGeometry.CreatePose(
            _canvasCoordinateWidth,
            _canvasCoordinateHeight,
            current,
            deviceScale);
        _hitMap = SpiderHitMap.Create(pose);
        _contentBoundsPhysical = new PixelRect(
            (int)Math.Floor(pose.ContentBounds.Left),
            (int)Math.Floor(pose.ContentBounds.Top),
            Math.Max(1, (int)Math.Ceiling(pose.ContentBounds.Width)),
            Math.Max(1, (int)Math.Ceiling(pose.ContentBounds.Height)));
        _scene.Draw(canvas, current, pose);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResizeForScale(Snapshot.Scale);
        UpdatePauseButtonVisual();
        UpdateVisibilityPause();
        UpdateRenderingSubscription();
        _locomotionController.ResetToCurrentPosition();
        UpdateMovementSubscription();
        SpiderCanvas.InvalidateVisual();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateVisibilityPause();
        if (!IsVisible)
        {
            StopThoughtBubble();
            FinalizePointerInteraction(null, complete: false);
        }

        UpdateRenderingSubscription();
        UpdateMovementSubscription();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateVisibilityPause();
        if (WindowState == WindowState.Minimized)
        {
            FinalizePointerInteraction(null, complete: false);
        }

        UpdateRenderingSubscription();
        UpdateMovementSubscription();
    }

    private void UpdateVisibilityPause() =>
        SetHiddenState(!IsVisible || WindowState == WindowState.Minimized);

    private void SetHiddenState(bool hidden)
    {
        _animationClock.SetPaused(AnimationPauseReason.Hidden, hidden);
        _runtime.SetHidden(hidden);
    }

    private void UpdateRenderingSubscription()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            SpiderCanvas.InvalidateVisual();
        }
    }

    private void UpdateMovementSubscription()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            _movementClock.Restart();
            _movementTimer.Start();
        }
        else
        {
            _movementTimer.Stop();
            _movementClock.Restart();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        FinalizePointerInteraction(null, complete: false);
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            StopThoughtBubble();
            _movementTimer.Stop();
            _movementTimer.Tick -= OnMovementTimerTick;
            _thoughtTimer.Tick -= OnThoughtTimerTick;
            _runtime.SnapshotChanged -= OnRuntimeSnapshotChanged;
            _runtime.Dispose();
            _scene.Dispose();
        }
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_menuOpen)
        {
            Cursor = System.Windows.Input.Cursors.SizeAll;
        }
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
        FinalizePointerInteraction(
            new PixelPoint((int)Math.Round(screen.X), (int)Math.Round(screen.Y)),
            complete: true);
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) =>
        FinalizePointerInteraction(null, complete: false);

    private void OnDeactivated(object? sender, EventArgs e) =>
        FinalizePointerInteraction(null, complete: false);

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        SetMenuOpen(true);
        e.Handled = true;
    }

    private void OnPetButtonClick(object sender, RoutedEventArgs e)
    {
        SafeEventDispatcher.Publish(PetCommandRequested, ReportInputError);
        SafeEventDispatcher.Publish(
            InteractionOccurred,
            new PetInteractionOccurred(_petId, "pet"),
            ReportInputError);
        SetMenuOpen(false);
    }

    private void OnPauseButtonClick(object sender, RoutedEventArgs e)
    {
        SetUserPaused(!_userPaused);
        SafeEventDispatcher.Publish(UserPauseChanged, _userPaused, ReportInputError);
        SetMenuOpen(false);
    }

    private void OnSleepButtonClick(object sender, RoutedEventArgs e)
    {
        SafeEventDispatcher.Publish(
            SkillCommandRequested,
            new BehaviorKey(BuiltInBehaviorKeys.Sleep),
            ReportInputError);
        SetMenuOpen(false);
    }

    private void OnAiInsightButtonClick(object sender, RoutedEventArgs e)
    {
        SafeEventDispatcher.Publish(AiInsightRequested, ReportInputError);
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

    private void UpdatePauseButtonVisual()
    {
        var tooltip = _userPaused ? "恢复自主行动" : "暂停自主行动";
        PauseMenuButton.ToolTip = tooltip;
        AutomationProperties.SetName(PauseMenuButton, tooltip);
        PauseIconPath.Data = Geometry.Parse(
            _userPaused
                ? "M6,4 L20,12 L6,20 Z"
                : "M9,5 L9,19 M15,5 L15,19");
    }

    private void FinalizePointerInteraction(PixelPoint? pointer, bool complete)
    {
        var wasDragging = _interactionController.IsDragging;
        try
        {
            if (complete && pointer is { } finalPoint)
            {
                wasDragging |= _interactionFinalizer.Complete(finalPoint);
            }
            else
            {
                wasDragging |= _interactionFinalizer.Cancel();
            }
        }
        finally
        {
            if (Mouse.Captured == this)
            {
                Mouse.Capture(null);
            }

            if (wasDragging)
            {
                RestoreToVisibleWorkArea();
                _locomotionController.ResetToCurrentPosition();
            }
        }
    }

    private void ResizeForScale(float scale)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => ResizeForScale(scale));
            return;
        }

        var next = OverlaySizePolicy.Calculate(scale);
        if (!IsLoaded)
        {
            Width = next.Width;
            Height = next.Height;
            return;
        }

        var dpi = GetDpi();
        var origin = GetPhysicalWindowOrigin();
        var currentSize = GetPhysicalWindowSize();
        var current = new PixelRect(
            origin.X,
            origin.Y,
            Math.Max(1, currentSize.Width),
            Math.Max(1, currentSize.Height));
        var centered = OverlaySizePolicy.KeepCenter(
            current,
            new OverlaySize(
                next.Width * dpi.DpiScaleX,
                next.Height * dpi.DpiScaleY));
        Width = next.Width;
        Height = next.Height;
        ApplyPhysicalBounds(_displayBounds.RestoreToVisibleWorkArea(centered), dpi);
        SpiderCanvas.InvalidateVisual();
    }

    private static void ReportInputError(Exception exception) =>
        Trace.TraceError("桌面互动回调失败：{0}", exception);

    private void OnThoughtTimerTick(object? sender, EventArgs e) => StopThoughtBubble();

    private void StopThoughtBubble()
    {
        _thoughtTimer.Stop();
        ThoughtBubble.Visibility = Visibility.Collapsed;
    }

    private static string NeedBand(double value) =>
        value < 0.34 ? "低" : value < 0.67 ? "中" : "高";

    private static string MoodName(PetMood mood) => mood switch
    {
        PetMood.Happy => "愉悦",
        PetMood.Curious => "好奇",
        PetMood.Hungry => "饥饿",
        PetMood.Sleepy => "困倦",
        PetMood.Hurt => "受伤",
        PetMood.Alert => "警觉",
        PetMood.Angry => "生气",
        _ => "平静"
    };

    private static string ModeName(PetMode mode) =>
        mode == PetMode.Berserk ? "狂暴" : "常态";

    private void OnRuntimeSnapshotChanged(object? sender, RenderSnapshot snapshot)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => OnRuntimeSnapshotChanged(sender, snapshot));
            return;
        }

        Snapshot = snapshot with { LookX = Snapshot.LookX, LookY = Snapshot.LookY };
        _locomotionController.UpdateSnapshot(Snapshot);
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            SpiderCanvas.InvalidateVisual();
        }
    }

    private void OnMovementTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _movementClock.Elapsed;
        _movementClock.Restart();
        _locomotionController.SetPaused(
            _pauseCoordinator.EffectivePaused
            || _menuOpen
            || !IsVisible
            || WindowState == WindowState.Minimized);
        try
        {
            _locomotionController.Tick(elapsed);
        }
        catch (Exception exception)
        {
            Trace.TraceError("桌宠自主移动失败：{0}", exception);
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
                (int)Math.Round((double.IsFinite(Left) ? Left : 0) * GetDpi().DpiScaleX),
                (int)Math.Round((double.IsFinite(Top) ? Top : 0) * GetDpi().DpiScaleY));
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
        FinalizePointerInteraction(null, complete: false);
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
