using System.Windows;
using Honey.Desktop.SingleInstance;
using Honey.Desktop.Settings;
using Honey.Desktop.Runtime;
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
    private SettingsApplicationCoordinator? _settingsCoordinator;
    private FocusModeService? _focusMode;
    private SqlitePetStateStore? _petStateStore;
    private IDisposable? _overlayFocusLease;
    private IDisposable? _settingsFocusLease;
    private AppSettings _settings = new();
    private System.Threading.Timer? _saveTimer;
    private int _shuttingDown;
    private int _shutdownPrepared;

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
        try
        {
            _settings = await _settingsStore.LoadAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            Trace.TraceError("设置读取失败，使用内存默认值继续：{0}", exception);
            _settings = new AppSettings();
        }
        _autoStart = new AutoStartService();
        _settingsCoordinator = new SettingsApplicationCoordinator(_settingsStore, _autoStart);
        _focusMode = new FocusModeService();
        _petStateStore = new SqlitePetStateStore(paths.DatabasePath);
        var pack = new WhiteJadeSpiderPack();
        var initial = await PetStateBootstrapper.LoadOrCreateAsync(
            _petStateStore,
            pack,
            DateTimeOffset.UtcNow,
            exception => Trace.TraceError(
                "存档读取失败，原记录保持不变并以固定主宠继续：{0}",
                exception),
            CancellationToken.None);

        var displayBounds = new DisplayBoundsService();
        _overlayWindow = new OverlayWindow(
            displayBounds,
            new OverlayHitTestPolicy(),
            initial,
            _settings);
        _overlayWindow.UserPauseChanged += OnOverlayPauseChanged;
        _overlayWindow.PetCommandRequested += OnPetCommandRequested;
        _overlayWindow.SkillCommandRequested += OnSkillCommandRequested;
        _overlayWindow.ModeToggleRequested += OnModeToggleRequested;
        _overlayWindow.SourceInitialized += (_, _) =>
        {
            _overlayFocusLease?.Dispose();
            _overlayFocusLease = _focusMode?.RegisterOwnWindow(
                new WindowInteropHelper(_overlayWindow).Handle);
        };
        _overlayWindow.Closed += (_, _) =>
        {
            _overlayFocusLease?.Dispose();
            _overlayFocusLease = null;
        };
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
        PrepareShutdownAsync().GetAwaiter().GetResult();
        _focusMode?.Dispose();
        _focusMode = null;

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
        _settingsWindow.SourceInitialized += (_, _) =>
        {
            _settingsFocusLease?.Dispose();
            _settingsFocusLease = _focusMode?.RegisterOwnWindow(
                new WindowInteropHelper(_settingsWindow).Handle);
        };
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsFocusLease?.Dispose();
            _settingsFocusLease = null;
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async void OnExitRequested(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _shuttingDown, 1) != 0)
        {
            return;
        }

        await PrepareShutdownAsync();
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
        if (_settingsCoordinator is null)
        {
            throw new InvalidOperationException("设置协调器尚未初始化。");
        }

        await _settingsCoordinator.ApplyAsync(
            _settings,
            normalized,
            Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定 Honey.exe 路径。"),
            cancellationToken);
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

    private void OnPetCommandRequested() => _overlayWindow?.RuntimeCommands.Pet();

    private void OnSkillCommandRequested(Honey.Domain.Behavior.BehaviorKey key) =>
        _overlayWindow?.RuntimeCommands.RequestSkill(key);

    private void OnModeToggleRequested()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        var preference = _overlayWindow.RuntimeCommands.ToggleMode();
        _settings = _settings with { ModePreference = preference };
        _ = SaveSettingsSafelyAsync();
    }

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

    private async Task PrepareShutdownAsync()
    {
        if (Interlocked.Exchange(ref _shutdownPrepared, 1) != 0)
        {
            return;
        }

        _saveTimer?.Dispose();
        _saveTimer = null;
        if (_overlayWindow is not null)
        {
            await _overlayWindow.StopRuntimeAsync();
        }
        _overlayFocusLease?.Dispose();
        _overlayFocusLease = null;
        _settingsFocusLease?.Dispose();
        _settingsFocusLease = null;
        if (_focusMode is not null)
        {
            await _focusMode.StopAsync();
        }

        await SavePetStateSafelyAsync();
    }
}
