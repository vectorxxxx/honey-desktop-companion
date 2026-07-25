using System.Diagnostics;
using Honey.Content.WhiteJadeSpider;
using Honey.Desktop.Settings;
using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Rendering;
using Honey.Simulation;

namespace Honey.Desktop.Runtime;

public sealed class PetRuntimeController : IDisposable
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
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly System.Threading.Timer _timer;
    private DateTimeOffset _lastTick;
    private TimeSpan _skillClock;
    private TimeSpan _nextIntent;
    private TimeSpan _simulationAccumulator;
    private TimeSpan _skillAccumulator;
    private TimeSpan _backgroundAccumulator;
    private TimeSpan _animationElapsed;
    private SkillFrame _currentFrame = new(
        new BehaviorKey(BuiltInBehaviorKeys.Observe),
        string.Empty,
        0,
        0,
        false);
    private PetState _state;
    private AppSettings _settings;
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
        _state = state;
        _settings = settings.Normalize();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = random ?? Random.Shared;
        _pack = new WhiteJadeSpiderPack();
        _simulation = new PetSimulation();
        _selector = new UtilityIntentSelector();
        _lastTick = _timeProvider.GetUtcNow();
        _nextIntent = TimeSpan.Zero;
        _timer = new System.Threading.Timer(
            _ => TickSafely(),
            null,
            startTimer ? TimeSpan.Zero : Timeout.InfiniteTimeSpan,
            startTimer ? TimeSpan.FromMilliseconds(16) : Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<RenderSnapshot>? SnapshotChanged;

    public PetState State
    {
        get { lock (_sync) return _state; }
    }

    public void ApplySettings(AppSettings settings)
    {
        lock (_sync)
        {
            _settings = settings.Normalize();
            _state = _state with
            {
                Scale = _settings.PetSize / 140d,
                Mode = PetRuntimePolicy.ApplyModePreference(_settings.ModePreference, _state.Mode)
            };
        }
    }

    public void SetPaused(bool paused) => _paused = paused;
    public void SetHidden(bool hidden) => _hidden = hidden;
    public void SetFocusActive(bool active) => _focusActive = active;

    public RenderSnapshot Tick(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "运行推进时长不得为负数。");
        }

        var applied = elapsed > MaximumCatchUp ? MaximumCatchUp : elapsed;
        RenderSnapshot snapshot;
        var publish = false;
        lock (_sync)
        {
            _animationElapsed = SaturatingAdd(_animationElapsed, applied);
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

            snapshot = new RenderSnapshot(
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
        }

        if (publish)
        {
            PublishSnapshot(snapshot);
        }

        return snapshot;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _timer.Dispose();
        }
    }

    private void AdvanceSimulation(TimeSpan step)
    {
        var simulation = _simulation.Step(_state, step, _random.NextDouble());
        _state = simulation.State;
        var mode = PetRuntimePolicy.ApplyModePreference(_settings.ModePreference, _state.Mode);
        _state = _state with
        {
            Mode = mode,
            Mood = PetRuntimePolicy.ResolveMood(_state.Needs, mode),
            Scale = _settings.PetSize / 140d
        };
    }

    private void AdvanceAutonomy(TimeSpan step)
    {
        _skillClock = SaturatingAdd(_skillClock, step);
        if (_player.IsPlaying)
        {
            _currentFrame = _player.Advance(step);
            return;
        }

        if (_skillClock < _nextIntent)
        {
            return;
        }

        var skill = SelectSkill(_skillClock);
        _player.Start(skill);
        _lastStarted[skill.Key] = _skillClock;
        _state = _state with { PreviousBehavior = skill.Key };
        _currentFrame = _player.Advance(TimeSpan.Zero);
        _nextIntent = SaturatingAdd(
            SaturatingAdd(_skillClock, skill.TimelineDuration),
            PetRuntimePolicy.IntentInterval(_settings.ActivityLevel, _focusActive));
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

    private void TickSafely()
    {
        if (Volatile.Read(ref _disposed) != 0
            || Interlocked.Exchange(ref _ticking, 1) != 0)
        {
            return;
        }

        try
        {
            var now = _timeProvider.GetUtcNow();
            var elapsed = now - _lastTick;
            var minimum = _hidden
                ? TimeSpan.FromSeconds(1)
                : _focusActive ? TimeSpan.FromMilliseconds(33) : TimeSpan.FromMilliseconds(16);
            if (elapsed < minimum)
            {
                return;
            }

            _lastTick = now;
            Tick(elapsed);
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

    private static TimeSpan SaturatingAdd(TimeSpan left, TimeSpan right) =>
        right.Ticks > TimeSpan.MaxValue.Ticks - left.Ticks
            ? TimeSpan.MaxValue
            : left + right;
}
