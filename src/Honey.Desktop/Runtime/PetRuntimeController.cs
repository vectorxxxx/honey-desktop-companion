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
    private readonly object _sync = new();
    private readonly WhiteJadeSpiderPack _pack;
    private readonly PetSimulation _simulation;
    private readonly UtilityIntentSelector _selector;
    private readonly SkillPlayer _player = new();
    private readonly Dictionary<BehaviorKey, DateTimeOffset> _lastStarted = [];
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly System.Threading.Timer _timer;
    private DateTimeOffset _lastTick;
    private DateTimeOffset _nextIntent;
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
        _nextIntent = _lastTick;
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
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var simulationElapsed = _hidden
                ? TimeSpan.FromSeconds(Math.Min(elapsed.TotalSeconds, 1))
                : elapsed;
            var simulation = _simulation.Step(_state, simulationElapsed, _random.NextDouble());
            _state = simulation.State;
            var mode = PetRuntimePolicy.ApplyModePreference(_settings.ModePreference, _state.Mode);
            _state = _state with
            {
                Mode = mode,
                Mood = PetRuntimePolicy.ResolveMood(_state.Needs, mode),
                Scale = _settings.PetSize / 140d
            };

            SkillFrame frame;
            if (!_paused && _player.IsPlaying)
            {
                frame = _player.Advance(elapsed);
            }
            else if (!_paused && now >= _nextIntent)
            {
                var skill = SelectSkill(now);
                _player.Start(skill);
                _lastStarted[skill.Key] = now;
                _state = _state with { PreviousBehavior = skill.Key };
                frame = _player.Advance(TimeSpan.Zero);
                _nextIntent = now + skill.TimelineDuration
                    + PetRuntimePolicy.IntentInterval(_settings.ActivityLevel, _focusActive);
            }
            else
            {
                var key = _state.PreviousBehavior ?? new BehaviorKey(BuiltInBehaviorKeys.Observe);
                frame = new SkillFrame(key, "待机", 0, 0, false);
            }

            var snapshot = new RenderSnapshot(
                _state.Mode,
                _state.Mood,
                0,
                0,
                now.ToUnixTimeMilliseconds() / 1000d,
                (float)_state.Scale,
                frame.Key.Value,
                frame.Phase,
                frame.Progress,
                frame.TotalProgress).Normalize();
            return snapshot;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _timer.Dispose();
        }
    }

    private SkillDefinition SelectSkill(DateTimeOffset now)
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
            SnapshotChanged?.Invoke(this, Tick(elapsed));
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
}
