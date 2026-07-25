using System.Windows;
using Honey.Desktop.SingleInstance;
using Honey.Desktop.Settings;
using Honey.Desktop.Runtime;
using Honey.Desktop.Shutdown;
using Honey.Desktop.Tray;
using Honey.Content.WhiteJadeSpider;
using Honey.Integrations.Windows;
using Honey.Integrations.Ai;
using Honey.Integrations.Security;
using Honey.Persistence;
using System.Diagnostics;
using System.Net.Http;
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
    private DpapiSecretStore? _secretStore;
    private string? _aiApiKey;
    private HttpClient? _aiHttpClient;
    private AiCompanionCoordinator? _aiCoordinator;
    private AiRequestGate? _aiRequestGate;
    private AiConnectionTester? _aiConnectionTester;
    private FocusModeService? _focusMode;
    private SqlitePetStateStore? _petStateStore;
    private IPetRuntimeLifecycle? _runtimeLifecycle;
    private ShutdownCoordinator? _shutdownCoordinator;
    private IDisposable? _overlayFocusLease;
    private IDisposable? _settingsFocusLease;
    private AppSettings _settings = new();
    private System.Threading.Timer? _saveTimer;
    private readonly AsyncShutdownOperationQueue _periodicSaveQueue = new();
    private int _shuttingDown;
    private int _sessionEndingBridgeUsed;
    private static readonly TimeSpan BlockingShutdownTimeout = TimeSpan.FromSeconds(4);

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
        _secretStore = new DpapiSecretStore();
        try
        {
            _aiApiKey = await _secretStore.LoadAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            Trace.TraceError("AI 密钥读取失败，保持 AI 本地降级：{0}", exception);
        }
        _aiHttpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _aiRequestGate = new AiRequestGate();
        _aiConnectionTester = new AiConnectionTester(_aiRequestGate);
        _aiCoordinator = new AiCompanionCoordinator(CreateAiProvider, _aiRequestGate);
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
        _runtimeLifecycle = _overlayWindow.RuntimeLifecycle;
        _overlayWindow.UserPauseChanged += OnOverlayPauseChanged;
        _overlayWindow.PetCommandRequested += OnPetCommandRequested;
        _overlayWindow.SkillCommandRequested += OnSkillCommandRequested;
        _overlayWindow.AiInsightRequested += OnAiInsightRequested;
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

        _shutdownCoordinator = new ShutdownCoordinator(
            PrepareShutdownCoreAsync,
            () => Dispatcher.InvokeAsync(() =>
            {
                _overlayWindow?.CloseForExit();
                Shutdown();
            }).Task,
            ReportShutdownError);

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _singleInstance.StartListening(HandleSingleInstanceCommandAsync);
        _focusMode.Changed += OnFocusSnapshotChanged;
        _saveTimer = new System.Threading.Timer(
            _ => _periodicSaveQueue.TryEnqueue(SavePetStateSafelyAsync),
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
        var preparationCompleted =
            Volatile.Read(ref _sessionEndingBridgeUsed) != 0
                ? _shutdownCoordinator?.IsPreparationCompleted == true
                : TryPrepareForBlockingExit("直接退出回退");
        if (preparationCompleted)
        {
            _focusMode?.Dispose();
        }
        _focusMode = null;

        _overlayFocusLease?.Dispose();
        _overlayFocusLease = null;
        _settingsFocusLease?.Dispose();
        _settingsFocusLease = null;
        _settingsWindow = null;
        _aiCoordinator = null;
        _aiConnectionTester = null;
        _aiRequestGate = null;
        _aiHttpClient?.Dispose();
        _aiHttpClient = null;
        _aiApiKey = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _showCommandDispatcher = null;
        _overlayWindow = null;
        _runtimeLifecycle = null;
        _singleInstance = null;
        _shutdownCoordinator = null;

        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Interlocked.Exchange(ref _shuttingDown, 1);
        Interlocked.Exchange(ref _sessionEndingBridgeUsed, 1);
        TryPrepareForBlockingExit("Windows 会话结束");
        base.OnSessionEnding(e);
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

        _settingsWindow = new SettingsWindow(
            _settings,
            !string.IsNullOrWhiteSpace(_aiApiKey),
            ApplyAiSettingsAsync,
            TestAiConnectionAsync)
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

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _ = RequestShutdownAsync();
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

    private async Task ApplyAiSettingsAsync(
        AiSettingsSubmission submission,
        CancellationToken cancellationToken)
    {
        if (_secretStore is null)
        {
            throw new InvalidOperationException("安全密钥存储尚未初始化。");
        }

        var coordinator = new AiSettingsSaveCoordinator(
            _secretStore,
            ApplySettingsAsync);
        _aiApiKey = await coordinator.ApplyAsync(
            _aiApiKey,
            submission,
            cancellationToken);
    }

    private async Task<string> TestAiConnectionAsync(
        AppSettings settings,
        string? enteredKey,
        CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(enteredKey) ? _aiApiKey : enteredKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return "测试失败：请先填写或保存 API 密钥。";
        }

        try
        {
            if (_aiHttpClient is null || _aiConnectionTester is null)
            {
                return "测试失败：AI 网络组件尚未初始化。";
            }

            var provider = new OpenAiCompatibleProvider(
                _aiHttpClient,
                new AiOptions(
                    settings.AiEndpoint,
                    settings.AiModel,
                    key,
                    AiOptions.DefaultTimeout));
            var result = await _aiConnectionTester.TestAsync(
                provider,
                new AiCompanionRequest(
                    "请用一句简短中文回应连接测试。",
                    "情绪：平静；形态：常态；行为：观察；需求：均衡",
                    []),
                cancellationToken);
            return result.Available
                ? "连接成功，AI 个性增强可用。"
                : $"连接未成功：{FailureMessage(result.FailureCode)}";
        }
        catch (ArgumentException exception)
        {
            return $"配置无效：{exception.Message}";
        }
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

    private void OnAiInsightRequested()
    {
        _ = RequestAiInsightAsync();
    }

    private async Task RequestAiInsightAsync()
    {
        if (_overlayWindow is null || _aiCoordinator is null)
        {
            return;
        }

        var result = await _aiCoordinator.RequestAsync(
            new AiCompanionRequest(
                "请结合你现在的状态，给我一句简短灵感并建议一个可选动作。",
                _overlayWindow.CreateAiStateSummary(),
                []),
            intent =>
            {
                var accepted = _overlayWindow.RuntimeCommands.TryRequestAiSkill(
                    new Honey.Domain.Behavior.BehaviorKey(intent));
                if (!accepted)
                {
                    Trace.TraceInformation("AI 建议因技能冷却或运行状态被忽略。");
                }
            },
            CancellationToken.None);
        _overlayWindow.ShowThought(
            result.Available
                ? result.Text ?? "小玉安静地陪在你身边。"
                : FailureMessage(result.FailureCode));
    }

    private IAiCompanionProvider? CreateAiProvider()
    {
        if (!_settings.AiEnabled
            || string.IsNullOrWhiteSpace(_aiApiKey)
            || _aiHttpClient is null)
        {
            return null;
        }

        try
        {
            return new OpenAiCompatibleProvider(
                _aiHttpClient,
                new AiOptions(
                    _settings.AiEndpoint,
                    _settings.AiModel,
                    _aiApiKey,
                    AiOptions.DefaultTimeout));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string FailureMessage(string? code) => code switch
    {
        "disabled" => "AI 尚未启用或未配置密钥，我先按自己的节奏玩耍。",
        "busy" => "我正在想上一件事，请稍等一下。",
        "cooldown" => "请求刚刚完成，请稍后再试。",
        "timeout" => "远方回应有些慢，我先继续探索。",
        "auth" => "AI 密钥未通过验证，请到设置中检查。",
        "rate_limited" => "今天的灵感稍显拥挤，稍后再试。",
        "server_error" or "network" => "暂时联系不上灵感源，我仍会照常活动。",
        _ => "这次没有得到灵感，我先继续自己的探索。"
    };

    private async Task SavePetStateSafelyAsync()
    {
        try
        {
            if (_runtimeLifecycle is not null && _petStateStore is not null)
            {
                await _petStateStore.SaveAsync(
                        _runtimeLifecycle.State,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("周期存档失败：{0}", exception);
        }
    }

    private Task RequestShutdownAsync()
    {
        Interlocked.Exchange(ref _shuttingDown, 1);
        return _shutdownCoordinator?.RequestShutdownAsync() ?? Task.CompletedTask;
    }

    private async Task PrepareShutdownCoreAsync()
    {
        var saveTimer = Interlocked.Exchange(ref _saveTimer, null);
        if (saveTimer is not null)
        {
            try
            {
                await saveTimer.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
                when (!ShutdownExceptionPolicy.IsFatal(exception))
            {
                ReportShutdownError(exception);
            }
        }

        try
        {
            await _periodicSaveQueue.StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
            when (!ShutdownExceptionPolicy.IsFatal(exception))
        {
            ReportShutdownError(exception);
        }

        var runtime = _runtimeLifecycle;
        var focusMode = _focusMode;
        var petStateStore = _petStateStore;
        var singleInstance = _singleInstance;

        if (runtime is not null)
        {
            try
            {
                await runtime.StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
                when (!ShutdownExceptionPolicy.IsFatal(exception))
            {
                ReportShutdownError(exception);
            }
        }

        if (focusMode is not null)
        {
            try
            {
                await focusMode.StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
                when (!ShutdownExceptionPolicy.IsFatal(exception))
            {
                ReportShutdownError(exception);
            }
        }

        if (runtime is not null && petStateStore is not null)
        {
            try
            {
                await petStateStore.SaveAsync(
                        runtime.State,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
                when (!ShutdownExceptionPolicy.IsFatal(exception))
            {
                ReportShutdownError(exception);
            }
        }

        if (singleInstance is not null)
        {
            try
            {
                await singleInstance.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
                when (!ShutdownExceptionPolicy.IsFatal(exception))
            {
                ReportShutdownError(exception);
            }
        }
    }

    private bool TryPrepareForBlockingExit(string path)
    {
        if (_shutdownCoordinator is null)
        {
            return true;
        }

        var completed = BlockingShutdownBridge.TryRun(
            _shutdownCoordinator.PrepareAsync,
            BlockingShutdownTimeout,
            ReportShutdownError);
        if (!completed)
        {
            Trace.TraceWarning(
                "{0}未能在 {1} 内完成关键退出准备，继续释放 UI 资源。",
                path,
                BlockingShutdownTimeout);
        }

        return completed;
    }

    private static void ReportShutdownError(Exception exception) =>
        Trace.TraceError("应用退出错误：{0}", exception);
}
