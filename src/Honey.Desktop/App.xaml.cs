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
using System.Net;
using System.Windows.Interop;
using Microsoft.Win32;
using Honey.Desktop.Startup;
using System.IO;
using Honey.Desktop.Diagnostics;
using Honey.Desktop.Status;

namespace Honey.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ShowCommandDispatcher? _showCommandDispatcher;
    private OverlayWindow? _overlayWindow;
    private TrayIconService? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private PetCodexWindow? _petCodexWindow;
    private SettingsStore? _settingsStore;
    private AutoStartService? _autoStart;
    private SettingsApplicationCoordinator? _settingsCoordinator;
    private DpapiSecretStore? _secretStore;
    private BoundAiSecret? _aiSecret;
    private HttpClient? _aiHttpClient;
    private AiCompanionCoordinator? _aiCoordinator;
    private AiRequestGate? _aiRequestGate;
    private AiConnectionTester? _aiConnectionTester;
    private readonly AiOperationCoordinator _aiOperations = new();
    private readonly SemaphoreSlim _aiSettingsSaveGate = new(1, 1);
    private FocusModeService? _focusMode;
    private SqlitePetStateStore? _petStateStore;
    private IPetRuntimeLifecycle? _runtimeLifecycle;
    private ShutdownCoordinator? _shutdownCoordinator;
    private IDisposable? _overlayFocusLease;
    private IDisposable? _settingsFocusLease;
    private IDisposable? _petCodexFocusLease;
    private AppSettings _settings = new();
    private System.Threading.Timer? _saveTimer;
    private readonly AsyncShutdownOperationQueue _periodicSaveQueue = new();
    private int _shuttingDown;
    private int _sessionEndingBridgeUsed;
    private static readonly TimeSpan BlockingShutdownTimeout = TimeSpan.FromSeconds(4);
    private TextWriterTraceListener? _logListener;
    private CancellationTokenSource? _startupCancellation;
    private StartupCommandInbox? _startupCommandInbox;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupArguments startup;
        try
        {
            startup = StartupArguments.Parse(e.Args);
        }
        catch (ArgumentException exception)
        {
            Trace.TraceError("启动参数无效：{0}", exception.Message);
            Shutdown(2);
            return;
        }

        var environmentDataRoot = Environment.GetEnvironmentVariable("HONEY_DATA_ROOT");
        var configuredDataRoot = startup.DataRoot
            ?? (string.IsNullOrWhiteSpace(environmentDataRoot) ? null : environmentDataRoot);
        AppDataPaths paths;
        try
        {
            paths = new AppDataPaths(configuredDataRoot);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or UnauthorizedAccessException)
        {
            Trace.TraceError("数据目录无效：{0}", exception);
            Shutdown(2);
            return;
        }
        if (startup.Command == StartupCommand.VerifyData)
        {
            try
            {
                await SqliteArchiveVerifier.VerifyAsync(
                    paths.DatabasePath,
                    CancellationToken.None);
                Shutdown(0);
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                    or IOException
                    or UnauthorizedAccessException)
            {
                Trace.TraceError("存档自检失败：{0}", exception);
                Shutdown(4);
            }
            return;
        }

        SingleInstanceIdentity identity;
        try
        {
            identity = SingleInstanceIdentity.Create(paths.RootDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or System.ComponentModel.Win32Exception)
        {
            Trace.TraceError("无法安全解析单实例数据目录：{0}", exception);
            Shutdown(2);
            return;
        }
        _singleInstance = new SingleInstanceCoordinator(identity);
        if (!_singleInstance.IsPrimary)
        {
            var delivered = true;
            if (startup.Command != StartupCommand.Background)
            {
                var command = startup.Command == StartupCommand.Shutdown
                    ? SingleInstanceCommand.Shutdown
                    : SingleInstanceCommand.Show;
                var result = await _singleInstance.SendWithResultAsync(command);
                delivered = result.Success;
                if (delivered && command == SingleInstanceCommand.Shutdown)
                {
                    delivered = await WaitForPrimaryExitAsync(result.ServerProcessId);
                }
            }
            await _singleInstance.DisposeAsync();
            _singleInstance = null;
            Shutdown(delivered ? 0 : 3);
            return;
        }

        _startupCancellation = new CancellationTokenSource();
        _startupCommandInbox = new StartupCommandInbox(
            () => _startupCancellation.Cancel());
        _singleInstance.StartListening(_startupCommandInbox.HandleAsync);
        if (startup.Command == StartupCommand.Shutdown)
        {
            await _singleInstance.DisposeAsync();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var startupToken = _startupCancellation.Token;
        try
        {
        startupToken.ThrowIfCancellationRequested();
        paths.EnsureDirectories();
        _logListener = AppTraceLog.Create(paths.LogsDirectory);
        Trace.Listeners.Add(_logListener);
        Trace.AutoFlush = true;
        _secretStore = new DpapiSecretStore(
            Path.Combine(paths.RootDirectory, "secrets.json"));
        try
        {
            _aiSecret = await _secretStore.LoadBoundAsync(startupToken);
        }
        catch (OperationCanceledException) when (startupToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Trace.TraceError("AI 密钥读取失败，保持 AI 本地降级：{0}", exception);
        }
        _aiHttpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _aiRequestGate = new AiRequestGate();
        _aiConnectionTester = new AiConnectionTester(_aiRequestGate);
        _aiCoordinator = new AiCompanionCoordinator(CreateAiProvider, _aiRequestGate);
        _settingsStore = new SettingsStore(
            Path.Combine(paths.RootDirectory, "settings.json"));
        try
        {
            _settings = await _settingsStore.LoadAsync(startupToken);
        }
        catch (OperationCanceledException) when (startupToken.IsCancellationRequested)
        {
            throw;
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
            startupToken);

        startupToken.ThrowIfCancellationRequested();
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
        _overlayWindow.StatusRequested += OnStatusRequested;
        _overlayWindow.RuntimeStatus.StatusChanged += OnPetStatusChanged;
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
            action => Dispatcher.InvokeAsync(action).Task,
            () => _overlayWindow?.ShowAndActivate());
        MainWindow = _overlayWindow;

        _trayIcon = new TrayIconService();
        _trayIcon.VisibilityToggleRequested += OnVisibilityToggleRequested;
        _trayIcon.PauseChanged += OnPauseChanged;
        _trayIcon.FocusModeChanged += OnFocusModeChanged;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.StatusRequested += OnTrayStatusRequested;
        _trayIcon.ExitRequested += OnExitRequested;
        _trayIcon.SetFocusMode(_settings.FocusMode);

        _shutdownCoordinator = new ShutdownCoordinator(
            PrepareShutdownCoreAsync,
            () => Dispatcher.InvokeAsync(() =>
            {
                _petCodexWindow?.Close();
                _overlayWindow?.CloseForExit();
                Shutdown();
            }).Task,
            ReportShutdownError);

        await _startupCommandInbox.AttachAsync(HandleReadySingleInstanceCommandAsync);
        startupToken.ThrowIfCancellationRequested();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _focusMode.Changed += OnFocusSnapshotChanged;
        _saveTimer = new System.Threading.Timer(
            _ => _periodicSaveQueue.TryEnqueue(SavePetStateSafelyAsync),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
        if (startup.Command != StartupCommand.Background)
        {
            _overlayWindow.Show();
            _overlayWindow.PlaceAtPrimaryBottomRight();
            if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
            {
                OnSettingsRequested(this, EventArgs.Empty);
            }
        }
        }
        catch (OperationCanceledException) when (startupToken.IsCancellationRequested)
        {
            if (_singleInstance is not null)
            {
                await _singleInstance.DisposeAsync();
                _singleInstance = null;
            }
            Shutdown();
        }
    }

    private static async Task<bool> WaitForPrimaryExitAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await process.WaitForExitAsync(timeout.Token);
            return true;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
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
        _petCodexFocusLease?.Dispose();
        _petCodexFocusLease = null;
        if (_overlayWindow is not null)
        {
            _overlayWindow.StatusRequested -= OnStatusRequested;
            _overlayWindow.RuntimeStatus.StatusChanged -= OnPetStatusChanged;
        }
        if (_trayIcon is not null)
        {
            _trayIcon.StatusRequested -= OnTrayStatusRequested;
        }
        _petCodexWindow?.Close();
        _petCodexWindow = null;
        _settingsWindow = null;
        _aiCoordinator = null;
        _aiConnectionTester = null;
        _aiRequestGate = null;
        _aiHttpClient?.Dispose();
        _aiHttpClient = null;
        _aiSecret = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _showCommandDispatcher = null;
        _overlayWindow = null;
        _runtimeLifecycle = null;
        _singleInstance = null;
        _shutdownCoordinator = null;
        _startupCommandInbox = null;
        _startupCancellation?.Dispose();
        _startupCancellation = null;
        if (_logListener is not null)
        {
            Trace.Listeners.Remove(_logListener);
            _logListener.Flush();
            _logListener.Dispose();
            _logListener = null;
        }

        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Interlocked.Exchange(ref _shuttingDown, 1);
        Interlocked.Exchange(ref _sessionEndingBridgeUsed, 1);
        TryPrepareForBlockingExit("Windows 会话结束");
        base.OnSessionEnding(e);
    }

    private Task HandleReadySingleInstanceCommandAsync(SingleInstanceCommand command)
    {
        if (command == SingleInstanceCommand.Shutdown)
        {
            if (!IsShuttingDown())
            {
                _ = Dispatcher.BeginInvoke(() => _ = RequestShutdownAsync());
            }

            return Task.CompletedTask;
        }

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

    private void OnTrayStatusRequested(object? sender, EventArgs e) =>
        OnStatusRequested();

    private void OnStatusRequested()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(OnStatusRequested);
            return;
        }

        if (_petCodexWindow is { IsVisible: true })
        {
            _petCodexWindow.Activate();
            return;
        }

        var source = _overlayWindow?.RuntimeStatus;
        if (source is null || IsShuttingDown())
        {
            return;
        }

        var window = new PetCodexWindow(source.Status);
        _petCodexWindow = window;
        window.SourceInitialized += (_, _) =>
        {
            _petCodexFocusLease?.Dispose();
            _petCodexFocusLease = _focusMode?.RegisterOwnWindow(
                new WindowInteropHelper(window).Handle);
        };
        window.Closed += (_, _) =>
        {
            _petCodexFocusLease?.Dispose();
            _petCodexFocusLease = null;
            if (ReferenceEquals(_petCodexWindow, window))
            {
                _petCodexWindow = null;
            }
        };
        window.Show();
        PlacePetCodexWithinWorkArea(window);
        window.Activate();
    }

    private void OnPetStatusChanged(object? sender, PetStatusSnapshot snapshot)
    {
        if (IsShuttingDown()
            || Dispatcher.HasShutdownStarted
            || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_petCodexWindow is not null)
            {
                _petCodexWindow.Update(snapshot);
            }
        });
    }

    private static void PlacePetCodexWithinWorkArea(PetCodexWindow window)
    {
        var workArea = SystemParameters.WorkArea;
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        window.Left = Math.Max(workArea.Left + 24, workArea.Right - width - 24);
        window.Top = Math.Max(workArea.Top + 24, workArea.Bottom - height - 24);
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
            _aiSecret is not null,
            AiConfigurationResolver.Resolve(
                true,
                _settings.AiEndpoint,
                _settings.AiModel,
                _settings.AiSecretBindingId,
                _aiSecret).Available,
            ApplyAiSettingsAsync,
            TestAiConnectionTrackedAsync)
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

        _ = Dispatcher.InvokeAsync(() =>
        {
            _overlayWindow?.RestoreToVisibleWorkArea();
            if (_petCodexWindow is { IsVisible: true } window)
            {
                PlacePetCodexWithinWorkArea(window);
            }
        });
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
            cancellationToken).ConfigureAwait(false);
        _settings = normalized;
        if (!IsShuttingDown()
            && !Dispatcher.HasShutdownStarted
            && !Dispatcher.HasShutdownFinished)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (IsShuttingDown())
                {
                    return;
                }

                _overlayWindow?.ApplySettings(normalized);
                _trayIcon?.SetFocusMode(normalized.FocusMode);
                _overlayWindow?.SetFocusActive(
                    normalized.FocusMode && (_focusMode?.IsFocusModeActive ?? false));
            });
        }
    }

    private async Task ApplyAiSettingsAsync(
        AiSettingsSubmission submission,
        CancellationToken cancellationToken)
    {
        await _aiOperations.RunAsync(
                token => ApplyAiSettingsCoreAsync(submission, token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ApplyAiSettingsCoreAsync(
        AiSettingsSubmission submission,
        CancellationToken cancellationToken)
    {
        if (_secretStore is null)
        {
            throw new InvalidOperationException("安全密钥存储尚未初始化。");
        }

        if (!await _aiSettingsSaveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("AI 设置正在保存，请稍候。");
        }

        try
        {
            var coordinator = new AiSettingsSaveCoordinator(
                _secretStore,
                ApplySettingsAsync);
            var result = await coordinator.ApplyAsync(
                    _aiSecret,
                    submission,
                    cancellationToken)
                .ConfigureAwait(false);
            _aiSecret = result.Secret;
            return true;
        }
        finally
        {
            _aiSettingsSaveGate.Release();
        }
    }

    private async Task<string> TestAiConnectionAsync(
        AppSettings settings,
        string? enteredKey,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_aiHttpClient is null || _aiConnectionTester is null)
            {
                return "测试失败：AI 网络组件尚未初始化。";
            }

            var validated = AiEndpointValidator.StrictValidate(
                settings.AiEndpoint,
                settings.AiModel);
            AiOptions options;
            if (!string.IsNullOrWhiteSpace(enteredKey))
            {
                options = new AiOptions(
                    validated.CanonicalEndpoint,
                    validated.Model,
                    enteredKey,
                    AiOptions.DefaultTimeout);
            }
            else
            {
                var resolved = AiConfigurationResolver.Resolve(
                    true,
                    settings.AiEndpoint,
                    settings.AiModel,
                    settings.AiSecretBindingId,
                    _aiSecret);
                if (!resolved.Available)
                {
                    return resolved.FailureCode == "binding_mismatch"
                        ? "密钥与配置不匹配，请重新保存。"
                        : "测试失败：请先填写或保存 API 密钥。";
                }

                options = resolved.Options!;
            }

            var provider = new OpenAiCompatibleProvider(_aiHttpClient, options);
            var result = await _aiConnectionTester.TestAsync(
                provider,
                new AiCompanionRequest(
                    "请用一句简短中文回应连接测试。",
                    "情绪：平静；形态：常态；行为：观察；需求：均衡",
                    []),
                cancellationToken).ConfigureAwait(false);
            return result.Available
                ? "连接成功，AI 个性增强可用。"
                : $"连接未成功：{FailureMessage(result.FailureCode)}";
        }
        catch (ArgumentException exception)
        {
            return $"配置无效：{exception.Message}";
        }
    }

    private Task<string> TestAiConnectionTrackedAsync(
        AppSettings settings,
        string? enteredKey,
        CancellationToken cancellationToken) =>
        _aiOperations.RunAsync(
            token => TestAiConnectionAsync(settings, enteredKey, token),
            cancellationToken);

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
        _ = _aiOperations.RunAsync(RequestAiInsightAsync);
    }

    private async Task RequestAiInsightAsync(CancellationToken cancellationToken)
    {
        var overlay = _overlayWindow;
        if (overlay is null || _aiCoordinator is null || IsShuttingDown())
        {
            return;
        }

        var result = await _aiCoordinator.RequestAsync(
            new AiCompanionRequest(
                "请结合你现在的状态，给我一句简短灵感并建议一个可选动作。",
                overlay.CreateAiStateSummary(),
                []),
            intent =>
            {
                if (IsShuttingDown() || !ReferenceEquals(_overlayWindow, overlay))
                {
                    return;
                }

                var decision = overlay.RuntimeCommands.TryRequestAiSkill(
                    new Honey.Domain.Behavior.BehaviorKey(intent));
                if (decision != AiSkillDecision.Accepted)
                {
                    Trace.TraceInformation("AI 建议未执行：{0}", decision);
                }
            },
            cancellationToken).ConfigureAwait(false);
        if (IsShuttingDown()
            || !ReferenceEquals(_overlayWindow, overlay)
            || Dispatcher.HasShutdownStarted
            || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!IsShuttingDown() && ReferenceEquals(_overlayWindow, overlay))
            {
                overlay.ShowThought(
                    result.Available
                        ? result.Text ?? "小玉安静地陪在你身边。"
                        : FailureMessage(result.FailureCode),
                    ThoughtSource.Ai);
            }
        });
    }

    private IAiCompanionProvider? CreateAiProvider()
    {
        if (_aiHttpClient is null)
        {
            return null;
        }

        var resolved = AiConfigurationResolver.Resolve(
            _settings.AiEnabled,
            _settings.AiEndpoint,
            _settings.AiModel,
            _settings.AiSecretBindingId,
            _aiSecret);
        if (!resolved.Available)
        {
            return null;
        }

        return new OpenAiCompatibleProvider(_aiHttpClient, resolved.Options!);
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
        try
        {
            await _aiOperations.StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
            when (!ShutdownExceptionPolicy.IsFatal(exception))
        {
            ReportShutdownError(exception);
        }

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
