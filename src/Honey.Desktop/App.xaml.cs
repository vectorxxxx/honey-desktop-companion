using System.Windows;
using Honey.Desktop.SingleInstance;
using Honey.Desktop.Tray;
using Honey.Integrations.Windows;
using Microsoft.Win32;

namespace Honey.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private OverlayWindow? _overlayWindow;
    private TrayIconService? _trayIcon;
    private bool _exitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            await _singleInstance.SendShowAsync();
            await _singleInstance.DisposeAsync();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var displayBounds = new DisplayBoundsService();
        _overlayWindow = new OverlayWindow(displayBounds, new OverlayHitTestPolicy());
        MainWindow = _overlayWindow;

        _trayIcon = new TrayIconService();
        _trayIcon.VisibilityToggleRequested += OnVisibilityToggleRequested;
        _trayIcon.PauseChanged += OnPauseChanged;
        _trayIcon.FocusModeChanged += OnFocusModeChanged;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.ExitRequested += OnExitRequested;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _singleInstance.StartListening(HandleSingleInstanceCommandAsync);
        _overlayWindow.Show();
        _overlayWindow.PlaceAtPrimaryBottomRight();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _overlayWindow = null;
        if (_singleInstance is not null)
        {
            _singleInstance.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _singleInstance = null;
        }

        base.OnExit(e);
    }

    private Task HandleSingleInstanceCommandAsync(SingleInstanceCommand command)
    {
        if (command != SingleInstanceCommand.Show || _exitRequested)
        {
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(() => _overlayWindow?.ShowAndActivate()).Task;
    }

    private void OnVisibilityToggleRequested(object? sender, EventArgs e)
    {
        if (_overlayWindow is null)
        {
            return;
        }

        if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
        }
        else
        {
            _overlayWindow.ShowAndActivate();
        }
    }

    private static void OnPauseChanged(object? sender, bool paused)
    {
        // Task 7 将该状态接入自主行为策略。
    }

    private static void OnFocusModeChanged(object? sender, bool focused)
    {
        // Task 7 将该状态接入专注模式策略。
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        System.Windows.MessageBox.Show(
            _overlayWindow,
            "设置面板将在后续版本接入。",
            "Honey 设置",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _exitRequested = true;
        _overlayWindow?.CloseForExit();
        Shutdown();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(() => _overlayWindow?.RestoreToVisibleWorkArea());
    }
}
