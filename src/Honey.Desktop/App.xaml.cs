using System.Windows;
using Honey.Desktop.SingleInstance;
using Honey.Desktop.Settings;
using Honey.Desktop.Tray;
using Honey.Content.WhiteJadeSpider;
using Honey.Integrations.Windows;
using Honey.Persistence;
using System.Diagnostics;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Honey.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ShowCommandDispatcher? _showCommandDispatcher;
    private OverlayWindow? _overlayWindow;
    private TrayIconService? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private SettingsStore? _settingsStore;
    private AutoStartService? _autoStart;
    private FocusModeService? _focusMode;
    private SqlitePetStateStore? _petStateStore;
    private AppSettings _settings = new();
    private System.Threading.Timer? _saveTimer;
    private int _shuttingDown;

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

        var paths = new AppDataPaths();
        paths.EnsureDirectories();
        _settingsStore = new SettingsStore();
        _settings = await _settingsStore.LoadAsync(CancellationToken.None);
        _autoStart = new AutoStartService();
        _focusMode = new FocusModeService();
        _petStateStore = new SqlitePetStateStore(paths.DatabasePath);
        var pack = new WhiteJadeSpiderPack();
        var stablePetId = Guid.Parse("9f0e9f5a-7056-4ba4-92dc-706ef1401186");
        var initial = pack.CreateInitialState(DateTimeOffset.UtcNow) with { PetId = stablePetId };
        try
        {
            await _petStateStore.InitializeAsync(CancellationToken.None);
            initial = await _petStateStore.LoadAsync(stablePetId, CancellationToken.None)
                ?? initial;
        }
        catch (Exception exception)
        {
            Trace.TraceError("存档读取失败，原记录保持不变并创建新灵兽：{0}", exception);
            initial = pack.CreateInitialState(DateTimeOffset.UtcNow);
        }

        var displayBounds = new DisplayBoundsService();
        _overlayWindow = new OverlayWindow(
            displayBounds,
            new OverlayHitTestPolicy(),
            initial,
            _settings);
        _overlayWindow.UserPauseChanged += OnOverlayPauseChanged;
        _focusMode.RegisterOwnWindow(new WindowInteropHelper(_overlayWindow).EnsureHandle());
        _showCommandDispatcher = new ShowCommandDispatcher(
            IsShuttingDown,
            action => _ = Dispatcher.BeginInvoke(action),
            () => _overlayWindow?.ShowAndActivate());
        MainWindow = _overlayWindow;

        _trayIcon = new TrayIconService();
        _trayIcon.VisibilityToggleRequested += OnVisibilityToggleRequested;
        _trayIcon.PauseChanged += OnPauseChanged;
        _trayIcon.FocusModeChanged += OnFocusModeChanged;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.ExitRequested += OnExitRequested;
        _trayIcon.SetFocusMode(_settings.FocusMode);

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _singleInstance.StartListening(HandleSingleInstanceCommandAsync);
        _focusMode.Changed += OnFocusSnapshotChanged;
        _saveTimer = new System.Threading.Timer(
            _ => _ = SavePetStateSafelyAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
        if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase))
        {
            _overlayWindow.Show();
            _overlayWindow.PlaceAtPrimaryBottomRight();
            if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
            {
                OnSettingsRequested(this, EventArgs.Empty);
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Interlocked.Exchange(ref _shuttingDown, 1);
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _focusMode?.Dispose();
        _focusMode = null;
        _saveTimer?.Dispose();
        _saveTimer = null;
        if (_overlayWindow is not null && _petStateStore is not null)
        {
            try
            {
                _petStateStore.SaveAsync(_overlayWindow.RuntimeState, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Trace.TraceError("退出存档失败：{0}", exception);
            }
        }

        _settingsWindow = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _showCommandDispatcher = null;
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
        if (command != SingleInstanceCommand.Show)
        {
            return Task.CompletedTask;
        }

        return _showCommandDispatcher?.Handle() ?? Task.CompletedTask;
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

    private void OnPauseChanged(object? sender, bool paused)
    {
        _overlayWindow?.SetUserPaused(paused);
    }

    private void OnFocusModeChanged(object? sender, bool focused)
    {
        _settings = _settings with { FocusMode = focused };
        _overlayWindow?.SetFocusActive(focused && (_focusMode?.IsFocusModeActive ?? false));
        _ = SaveSettingsSafelyAsync();
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, ApplySettingsAsync)
        {
            Owner = _overlayWindow?.IsVisible == true ? _overlayWindow : null
        };
        if (_focusMode is not null)
        {
            _focusMode.RegisterOwnWindow(new WindowInteropHelper(_settingsWindow).EnsureHandle());
        }
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _shuttingDown, 1);
        _overlayWindow?.CloseForExit();
        Shutdown();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsShuttingDown())
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(() => _overlayWindow?.RestoreToVisibleWorkArea());
    }

    private bool IsShuttingDown() =>
        Volatile.Read(ref _shuttingDown) != 0
        || Dispatcher.HasShutdownStarted
        || Dispatcher.HasShutdownFinished;

    private async Task ApplySettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var normalized = settings.Normalize();
        if (normalized.StartWithWindows != _settings.StartWithWindows)
        {
            if (normalized.StartWithWindows)
            {
                _autoStart?.Enable(Environment.ProcessPath
                    ?? throw new InvalidOperationException("无法确定 Honey.exe 路径。"));
            }
            else
            {
                _autoStart?.Disable();
            }
        }

        await (_settingsStore?.SaveAsync(normalized, cancellationToken)
            ?? Task.CompletedTask);
        _settings = normalized;
        _overlayWindow?.ApplySettings(normalized);
        _trayIcon?.SetFocusMode(normalized.FocusMode);
        _overlayWindow?.SetFocusActive(
            normalized.FocusMode && (_focusMode?.IsFocusModeActive ?? false));
    }

    private async Task SaveSettingsSafelyAsync()
    {
        try
        {
            if (_settingsStore is not null)
            {
                await _settingsStore.SaveAsync(_settings, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("设置保存失败：{0}", exception);
        }
    }

    private void OnFocusSnapshotChanged(object? sender, FocusSnapshot snapshot) =>
        Dispatcher.BeginInvoke(
            () => _overlayWindow?.SetFocusActive(_settings.FocusMode && snapshot.IsFocusModeActive));

    private void OnOverlayPauseChanged(bool paused) => _trayIcon?.SetPaused(paused);

    private async Task SavePetStateSafelyAsync()
    {
        try
        {
            if (_overlayWindow is not null && _petStateStore is not null)
            {
                await _petStateStore.SaveAsync(
                    _overlayWindow.RuntimeState,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("周期存档失败：{0}", exception);
        }
    }
}
