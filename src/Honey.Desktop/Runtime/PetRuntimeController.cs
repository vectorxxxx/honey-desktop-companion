using System.Diagnostics;
using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Settings;
using Honey.Desktop.Status;
using Honey.Domain.Activity;
using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Rendering;
using Honey.Simulation;

namespace Honey.Desktop.Runtime;

public enum AiSkillDecision
{
    Accepted,
    NotAllowed,
    Busy,
    Cooldown
}

public interface IPetRuntimeCommands
{
    void Pet();
    void RequestSkill(BehaviorKey key);
    AiSkillDecision TryRequestAiSkill(BehaviorKey key);
    string ToggleMode();
}

public interface IPetRuntimeLifecycle
{
    PetState State { get; }
    Task StopAsync();
}

public interface IPetStatusSource
{
    PetStatusSnapshot Status { get; }
    event EventHandler<PetStatusSnapshot>? StatusChanged;
}

public sealed class PetRuntimeController :
    IDisposable,
    IAsyncDisposable,
    IPetRuntimeCommands,
    IPetRuntimeLifecycle,
    IPetStatusSource
{
    public static readonly TimeSpan SimulationStep = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan SkillStep = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan BackgroundStep = TimeSpan.FromSeconds(1);
    // 超出预算的历史时间明确丢弃，避免恢复唤醒后出现追赶螺旋。
    public static readonly TimeSpan MaximumCatchUp = TimeSpan.FromSeconds(4);
    private readonly object _sync = new();
    private readonly WhiteJadeSpiderPack _pack;
    private readonly PetSimulation _simulation;
    private readonly UtilityIntentSelector _selector;
    private readonly SkillPlayer _player = new();
    private readonly Dictionary<BehaviorKey, TimeSpan> _lastStarted = [];
    private readonly PetActivityJournal _activityJournal = new();
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly System.Threading.Timer _timer;
    private readonly object _stopSync = new();
    private Task? _stopTask;
    private long _lastTimestamp;
    private TimeSpan _skillClock;
    private TimeSpan _nextIntent;
    private TimeSpan _simulationAccumulator;
    private TimeSpan _skillAccumulator;
    private TimeSpan _backgroundAccumulator;
    private TimeSpan _statusAccumulator;
    private TimeSpan _animationElapsed;
    private SkillFrame _currentFrame = new(
        new BehaviorKey(BuiltInBehaviorKeys.Observe),
        string.Empty,
        0,
        0,
        false);
    private TimeSpan _moodOverrideUntil;
    private PetState _state;
    private AppSettings _settings;
    private ActiveBehaviorState _activeBehavior;
    private bool _activeBehaviorOpen = true;
    private PetStatusSnapshot _status;
    private volatile bool _paused;
    private volatile bool _hidden;
    private volatile bool _focusActive;
    private int _ticking;
    private int _disposed;

    public PetRuntimeController(
        PetState state,
        AppSettings settings,
        TimeProvider? timeProvider = null,
        Random? random = null,
        bool startTimer = true)
    {
        _settings = settings.Normalize();
        _state = state with
        {
            Scale = _settings.PetSize / 140d,
            Mode = PetRuntimePolicy.ApplyModePreference(
                _settings.ModePreference,
                state.Mode)
        };
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = random ?? Random.Shared;
        _pack = new WhiteJadeSpiderPack();
        _simulation = new PetSimulation();
        _selector = new UtilityIntentSelector();
        _lastTimestamp = _timeProvider.GetTimestamp();
        _nextIntent = TimeSpan.Zero;
        var now = _timeProvider.GetUtcNow();
        _activeBehavior = new ActiveBehaviorState(
            new BehaviorKey(BuiltInBehaviorKeys.Observe),
            _currentFrame.Phase,
            BehaviorOrigin.SystemSchedule,
            now);
        _activityJournal.Append(new PetActivityEntry(
            now,
            _activeBehavior.Behavior,
            _activeBehavior.Origin,
            PetActivityOutcome.Started,
            "运行时初始化"));
        _status = PetStatusProjector.Project(
            _state,
            _activeBehavior,
            _activityJournal.Entries,
            now);
        _timer = new System.Threading.Timer(
            _ => TickSafely(),
            null,
            startTimer ? TimeSpan.Zero : Timeout.InfiniteTimeSpan,
            startTimer ? TimeSpan.FromMilliseconds(16) : Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<RenderSnapshot>? SnapshotChanged;
    public event EventHandler<PetStatusSnapshot>? StatusChanged;

    public PetState State
    {
        get { lock (_sync) return _state; }
    }

    public PetStatusSnapshot Status
    {
        get { lock (_sync) return _status; }
    }

    public AppSettings Settings
    {
        get { lock (_sync) return _settings; }
    }

    public void ApplySettings(AppSettings settings)
    {
        PetStatusSnapshot status;
        lock (_sync)
        {
            _settings = settings.Normalize();
            _state = _state with
            {
                Scale = _settings.PetSize / 140d,
                Mode = PetRuntimePolicy.ApplyModePreference(_settings.ModePreference, _state.Mode)
            };
            RefreshStatusLocked();
            status = _status;
        }

        PublishStatus(status);
    }

    public void SetPaused(bool paused) => _paused = paused;
    public void SetHidden(bool hidden) => _hidden = hidden;
    public void SetFocusActive(bool active) => _focusActive = active;

    public void Pet()
    {
        RenderSnapshot snapshot;
        PetStatusSnapshot status;
        lock (_sync)
        {
            _state = _state with
            {
                Needs = (_state.Needs with
                {
                    Affection = Math.Min(1, _state.Needs.Affection + 0.15),
                    Stress = Math.Max(0, _state.Needs.Stress - 0.1)
                }).Clamp(),
                Mood = PetMood.Happy
            };
            _moodOverrideUntil = SaturatingAdd(_skillClock, TimeSpan.FromSeconds(2));
            _activityJournal.Append(new PetActivityEntry(
                _timeProvider.GetUtcNow(),
                new BehaviorKey("pet"),
                BehaviorOrigin.UserInteraction,
                PetActivityOutcome.Started,
                "用户抚摸"));
            RefreshStatusLocked();
            snapshot = BuildSnapshot();
            status = _status;
        }

        PublishSnapshot(snapshot);
        PublishStatus(status);
    }

    public void RequestSkill(BehaviorKey key)
    {
        RenderSnapshot snapshot;
        PetStatusSnapshot status;
        lock (_sync)
        {
            var skill = WhiteJadeSpiderSkills.All.SingleOrDefault(item => item.Key == key)
                ?? throw new ArgumentException($"未知技能：{key}", nameof(key));
            snapshot = StartSkillLocked(skill, BehaviorOrigin.UserInteraction);
            status = _status;
        }

        PublishSnapshot(snapshot);
        PublishStatus(status);
    }

    public AiSkillDecision TryRequestAiSkill(BehaviorKey key)
    {
        RenderSnapshot? snapshot = null;
        PetStatusSnapshot status;
        AiSkillDecision decision;
        lock (_sync)
        {
            if (key.Value is not (
                    BuiltInBehaviorKeys.Observe
                    or BuiltInBehaviorKeys.Play
                    or BuiltInBehaviorKeys.Sleep
                    or BuiltInBehaviorKeys.Forage
                    or BuiltInBehaviorKeys.Web))
            {
                decision = RejectAiLocked(
                    key,
                    AiSkillDecision.NotAllowed,
                    "不在 AI 可执行白名单中");
                status = _status;
            }
            else if (_player.IsPlaying)
            {
                decision = RejectAiLocked(key, AiSkillDecision.Busy, "当前技能尚未结束");
                status = _status;
            }
            else
            {
                var skill = WhiteJadeSpiderSkills.All.Single(item => item.Key == key);
                if (_lastStarted.TryGetValue(key, out var last)
                    && _skillClock - last < skill.Cooldown)
                {
                    decision = RejectAiLocked(key, AiSkillDecision.Cooldown, "技能冷却中");
                    status = _status;
                }
                else
                {
                    snapshot = StartSkillLocked(
                        skill,
                        BehaviorOrigin.AiSuggestion,
                        "AI 建议已采纳");
                    decision = AiSkillDecision.Accepted;
                    status = _status;
                }
            }
        }

        if (snapshot is not null)
        {
            PublishSnapshot(snapshot);
        }

        PublishStatus(status);
        return decision;
    }

    public string ToggleMode()
    {
        RenderSnapshot snapshot;
        PetStatusSnapshot status;
        string preference;
        lock (_sync)
        {
            var mode = _state.Mode == PetMode.Normal ? PetMode.Berserk : PetMode.Normal;
            preference = mode == PetMode.Berserk ? "berserk" : "normal";
            _settings = _settings with { ModePreference = preference };
            _state = _state with
            {
                Mode = mode,
                Mood = mode == PetMode.Berserk ? PetMood.Angry : PetMood.Curious
            };
            _moodOverrideUntil = SaturatingAdd(_skillClock, TimeSpan.FromSeconds(2));
            _activityJournal.Append(new PetActivityEntry(
                _timeProvider.GetUtcNow(),
                new BehaviorKey("mode"),
                BehaviorOrigin.UserInteraction,
                PetActivityOutcome.Started,
                preference));
            RefreshStatusLocked();
            snapshot = BuildSnapshot();
            status = _status;
        }

        PublishSnapshot(snapshot);
        PublishStatus(status);
        return preference;
    }

    public RenderSnapshot Tick(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "运行推进时长不得为负数。");
        }

        var applied = elapsed > MaximumCatchUp ? MaximumCatchUp : elapsed;
        RenderSnapshot snapshot;
        PetStatusSnapshot status;
        var publish = false;
        var publishStatus = false;
        lock (_sync)
        {
            _animationElapsed = SaturatingAdd(_animationElapsed, applied);
            _statusAccumulator = SaturatingAdd(_statusAccumulator, applied);
            if (_paused || _hidden)
            {
                _backgroundAccumulator += applied;
                var updated = false;
                while (_backgroundAccumulator >= BackgroundStep)
                {
                    _backgroundAccumulator -= BackgroundStep;
                    AdvanceSimulation(BackgroundStep);
                    updated = true;
                }

                publish = updated && !_hidden;
            }
            else
            {
                _backgroundAccumulator = TimeSpan.Zero;
                _skillAccumulator += applied;
                while (_skillAccumulator >= SkillStep)
                {
                    _skillAccumulator -= SkillStep;
                    _simulationAccumulator += SkillStep;
                    if (_simulationAccumulator >= SimulationStep)
                    {
                        _simulationAccumulator -= SimulationStep;
                        AdvanceSimulation(SimulationStep);
                    }

                    AdvanceAutonomy(SkillStep);
                }

                publish = applied > TimeSpan.Zero;
            }

            RefreshStatusLocked();
            if (_statusAccumulator >= SimulationStep)
            {
                _statusAccumulator = TimeSpan.FromTicks(
                    _statusAccumulator.Ticks % SimulationStep.Ticks);
                publishStatus = true;
            }

            snapshot = BuildSnapshot();
            status = _status;
        }

        if (publish)
        {
            PublishSnapshot(snapshot);
        }

        if (publishStatus)
        {
            PublishStatus(status);
        }

        return snapshot;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public Task StopAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        lock (_stopSync)
        {
            return _stopTask ??= _timer.DisposeAsync().AsTask();
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private void AdvanceSimulation(TimeSpan step)
    {
        var simulation = _simulation.Step(
            _state,
            step,
            _random.NextDouble(),
            _activeBehavior.Behavior.Value == BuiltInBehaviorKeys.Sleep);
        _state = simulation.State;
        var mode = PetRuntimePolicy.ApplyModePreference(_settings.ModePreference, _state.Mode);
        _state = _state with
        {
            Mode = mode,
            Mood = _skillClock < _moodOverrideUntil
                ? _state.Mood
                : PetRuntimePolicy.ResolveMood(_state.Needs, mode),
            Scale = _settings.PetSize / 140d
        };
    }

    private RenderSnapshot StartSkillLocked(
        SkillDefinition skill,
        BehaviorOrigin origin,
        string? detail = null)
    {
        var now = _timeProvider.GetUtcNow();
        if (_activeBehaviorOpen)
        {
            _activityJournal.Append(new PetActivityEntry(
                now,
                _activeBehavior.Behavior,
                _activeBehavior.Origin,
                PetActivityOutcome.Interrupted,
                $"被 {skill.Key.Value} 替换"));
            _activeBehaviorOpen = false;
        }

        _player.Start(skill);
        _currentFrame = _player.Advance(TimeSpan.Zero);
        _lastStarted[skill.Key] = _skillClock;
        _activeBehavior = new ActiveBehaviorState(
            skill.Key,
            _currentFrame.Phase,
            origin,
            now);
        _activityJournal.Append(new PetActivityEntry(
            now,
            skill.Key,
            origin,
            PetActivityOutcome.Started,
            detail));
        _activeBehaviorOpen = true;
        _state = _state with
        {
            PreviousBehavior = skill.Key,
            Mood = skill.Key == new BehaviorKey(BuiltInBehaviorKeys.Sleep)
                ? PetMood.Sleepy
                : _state.Mood
        };
        _moodOverrideUntil = skill.Key == new BehaviorKey(BuiltInBehaviorKeys.Sleep)
            ? SaturatingAdd(_skillClock, skill.TimelineDuration)
            : _moodOverrideUntil;
        _nextIntent = SaturatingAdd(
            SaturatingAdd(_skillClock, skill.TimelineDuration),
            PetRuntimePolicy.IntentInterval(_settings.ActivityLevel, _focusActive));
        RefreshStatusLocked();
        return BuildSnapshot();
    }

    private void AdvanceAutonomy(TimeSpan step)
    {
        _skillClock = SaturatingAdd(_skillClock, step);
        if (_player.IsPlaying)
        {
            _currentFrame = _player.Advance(step);
            _activeBehavior = _activeBehavior with { Phase = _currentFrame.Phase };
            if (!_player.IsPlaying)
            {
                var now = _timeProvider.GetUtcNow();
                _activityJournal.Append(new PetActivityEntry(
                    now,
                    _activeBehavior.Behavior,
                    _activeBehavior.Origin,
                    PetActivityOutcome.Completed));
                _activeBehaviorOpen = false;
                EnterIdleLocked(now);
            }

            return;
        }

        if (_skillClock < _nextIntent)
        {
            return;
        }

        StartSkillLocked(SelectSkill(_skillClock), BehaviorOrigin.LocalAutonomy);
    }

    private void EnterIdleLocked(DateTimeOffset now)
    {
        var observe = new BehaviorKey(BuiltInBehaviorKeys.Observe);
        _currentFrame = new SkillFrame(observe, string.Empty, 0, 0, false);
        _activeBehavior = new ActiveBehaviorState(
            observe,
            string.Empty,
            BehaviorOrigin.SystemSchedule,
            now);
        _activityJournal.Append(new PetActivityEntry(
            now,
            observe,
            BehaviorOrigin.SystemSchedule,
            PetActivityOutcome.Started,
            "技能结束后待机观察"));
        _activeBehaviorOpen = true;
    }

    private SkillDefinition SelectSkill(TimeSpan now)
    {
        var candidates = _pack.Behaviors.Select(behavior =>
        {
            var remaining = _lastStarted.TryGetValue(behavior.Key, out var last)
                ? behavior.Cooldown - (now - last)
                : TimeSpan.Zero;
            return new IntentCandidate(behavior.Key, behavior.Score(_state), remaining);
        }).ToArray();
        var selected = _selector.Select(candidates, _state.PreviousBehavior, _random.NextDouble());
        return WhiteJadeSpiderSkills.All.Single(skill => skill.Key == selected.Key);
    }

    private AiSkillDecision RejectAiLocked(
        BehaviorKey key,
        AiSkillDecision decision,
        string reason)
    {
        _activityJournal.Append(new PetActivityEntry(
            _timeProvider.GetUtcNow(),
            key,
            BehaviorOrigin.AiSuggestion,
            PetActivityOutcome.Rejected,
            reason));
        RefreshStatusLocked();
        return decision;
    }

    private void RefreshStatusLocked()
    {
        _activeBehavior = _activeBehavior with { Phase = _currentFrame.Phase };
        _status = PetStatusProjector.Project(
            _state,
            _activeBehavior,
            _activityJournal.Entries,
            _timeProvider.GetUtcNow());
    }

    private void TickSafely()
    {
        if (Volatile.Read(ref _disposed) != 0
            || Interlocked.Exchange(ref _ticking, 1) != 0)
        {
            return;
        }

        try
        {
            TickFromClock();
        }
        catch (Exception exception)
        {
            Trace.TraceError("运行态推进失败：{0}", exception);
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    public void TickFromClock()
    {
        var now = _timeProvider.GetTimestamp();
        var elapsed = _timeProvider.GetElapsedTime(_lastTimestamp, now);
        var minimum = _hidden
            ? BackgroundStep
            : _focusActive ? TimeSpan.FromMilliseconds(33) : TimeSpan.FromMilliseconds(16);
        if (elapsed < minimum)
        {
            return;
        }

        _lastTimestamp = now;
        Tick(elapsed);
    }

    private void PublishSnapshot(RenderSnapshot snapshot)
    {
        var handlers = SnapshotChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList()
                     .Cast<EventHandler<RenderSnapshot>>())
        {
            try
            {
                subscriber(this, snapshot);
            }
            catch (Exception exception)
            {
                Trace.TraceError("运行态快照订阅者失败：{0}", exception);
            }
        }
    }

    private void PublishStatus(PetStatusSnapshot status)
    {
        var handlers = StatusChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList()
                     .Cast<EventHandler<PetStatusSnapshot>>())
        {
            try
            {
                subscriber(this, status);
            }
            catch (Exception exception)
            {
                Trace.TraceError("灵兽状态订阅者失败：{0}", exception);
            }
        }
    }

    private RenderSnapshot BuildSnapshot() =>
        new RenderSnapshot(
            _state.Mode,
            _state.Mood,
            0,
            0,
            _animationElapsed.TotalSeconds,
            (float)_state.Scale,
            _currentFrame.Key.Value,
            _currentFrame.Phase,
            _currentFrame.Progress,
            _currentFrame.TotalProgress).Normalize();

    private static TimeSpan SaturatingAdd(TimeSpan left, TimeSpan right) =>
        right.Ticks > TimeSpan.MaxValue.Ticks - left.Ticks
            ? TimeSpan.MaxValue
            : left + right;
}
