using Honey.Domain.Movement;
using Honey.Integrations.Windows;
using Honey.Rendering;

namespace Honey.Desktop.Movement;

public sealed class DesktopLocomotionController
{
    private readonly Func<PixelPoint> _getWindowOrigin;
    private readonly Func<PixelRect> _getContentBounds;
    private readonly Func<IReadOnlyList<PixelRect>> _getWorkAreas;
    private readonly Func<PixelPoint> _getPointer;
    private readonly Action<PixelPoint> _moveWindow;
    private readonly PetLocomotionProfile _profile;
    private readonly Random _random;
    private RenderSnapshot? _snapshot;
    private LocomotionPoint _target;
    private PixelPoint? _lastPointer;
    private double _pointerInterestSeconds;
    private TimeSpan _chaseRemaining;
    private TimeSpan _chaseCooldown;
    private bool _hasTarget;
    private bool _paused;
    private bool _enabled = true;
    private bool _allowCrossMonitor;

    public DesktopLocomotionController(
        Func<PixelPoint> getWindowOrigin,
        Func<PixelRect> getContentBounds,
        Func<IReadOnlyList<PixelRect>> getWorkAreas,
        Func<PixelPoint> getPointer,
        Action<PixelPoint> moveWindow,
        PetLocomotionProfile profile,
        Random? random = null)
    {
        _getWindowOrigin = getWindowOrigin ?? throw new ArgumentNullException(nameof(getWindowOrigin));
        _getContentBounds = getContentBounds ?? throw new ArgumentNullException(nameof(getContentBounds));
        _getWorkAreas = getWorkAreas ?? throw new ArgumentNullException(nameof(getWorkAreas));
        _getPointer = getPointer ?? throw new ArgumentNullException(nameof(getPointer));
        _moveWindow = moveWindow ?? throw new ArgumentNullException(nameof(moveWindow));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _random = random ?? Random.Shared;
        var origin = _getWindowOrigin();
        CurrentFrame = new LocomotionFrame(
            LocomotionState.At(new LocomotionPoint(origin.X, origin.Y)),
            0,
            true);
    }

    public LocomotionFrame CurrentFrame { get; private set; }
    public LocomotionIntent CurrentIntent { get; private set; } = LocomotionIntent.Idle;

    public void UpdateSnapshot(RenderSnapshot snapshot) =>
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    public void UpdateSettings(bool enabled, bool allowCrossMonitor)
    {
        _enabled = enabled;
        _allowCrossMonitor = allowCrossMonitor;
    }

    public void SetPaused(bool paused) => _paused = paused;

    public void ResetToCurrentPosition()
    {
        var origin = _getWindowOrigin();
        CurrentFrame = new LocomotionFrame(
            LocomotionState.At(new LocomotionPoint(origin.X, origin.Y)),
            0,
            true);
        _hasTarget = false;
        _chaseRemaining = TimeSpan.Zero;
        CurrentIntent = LocomotionIntent.Idle;
    }

    public void Tick(TimeSpan elapsed)
    {
        if (_paused || !_enabled || _snapshot is null || elapsed <= TimeSpan.Zero)
        {
            return;
        }

        var workAreas = _getWorkAreas()
            .Where(area => area.Width > 0 && area.Height > 0)
            .ToArray();
        if (workAreas.Length == 0)
        {
            return;
        }

        var content = _getContentBounds();
        var pointer = _getPointer();
        var pointerStimulus = MeasurePointer(pointer, content, elapsed);
        var snapshot = _snapshot;
        var resolvedIntent = PetLocomotionPolicy.Resolve(new PetLocomotionContext(
            snapshot.Behavior,
            snapshot.Phase,
            snapshot.Mood,
            snapshot.Mode,
            pointerStimulus));
        if (resolvedIntent == LocomotionIntent.ApproachPointer
            && _chaseRemaining <= TimeSpan.Zero)
        {
            _chaseRemaining = TimeSpan.FromSeconds(1.5);
            _pointerInterestSeconds = 0;
        }
        var intent = _chaseRemaining > TimeSpan.Zero
            && resolvedIntent is not LocomotionIntent.Anchor
                and not LocomotionIntent.RetreatPointer
            ? LocomotionIntent.ApproachPointer
            : resolvedIntent;
        if (_chaseRemaining > TimeSpan.Zero)
        {
            _chaseRemaining -= elapsed;
            if (_chaseRemaining <= TimeSpan.Zero)
            {
                _chaseRemaining = TimeSpan.Zero;
                _chaseCooldown = TimeSpan.FromSeconds(3);
            }
        }
        var intentChanged = intent != CurrentIntent;
        CurrentIntent = intent;

        var origin = CurrentFrame.State.Position;
        var currentContent = new PixelRect(
            (int)Math.Round(origin.X) + content.X,
            (int)Math.Round(origin.Y) + content.Y,
            content.Width,
            content.Height);
        var selectedArea = DesktopMovementBounds.SelectRoamingArea(
            currentContent,
            workAreas,
            _allowCrossMonitor,
            _hasTarget ? 0 : _random.NextDouble());
        var bounds = DesktopMovementBounds.Create(selectedArea, content);
        if (intentChanged || !_hasTarget || CurrentFrame.Arrived
            || intent is LocomotionIntent.ApproachPointer or LocomotionIntent.RetreatPointer)
        {
            _target = SelectTarget(intent, origin, pointer, content, bounds);
            _hasTarget = true;
        }

        CurrentFrame = PetLocomotionEngine.Step(
            CurrentFrame.State,
            new PetLocomotionInput(
                intent,
                _target,
                bounds,
                IsBerserk: snapshot.Mode == Honey.Domain.Model.PetMode.Berserk),
            _profile,
            elapsed);
        if (CurrentFrame.Arrived && intent is LocomotionIntent.Roam or LocomotionIntent.BehaviorTarget)
        {
            _hasTarget = false;
        }

        var next = new PixelPoint(
            (int)Math.Round(CurrentFrame.State.Position.X),
            (int)Math.Round(CurrentFrame.State.Position.Y));
        var current = _getWindowOrigin();
        if (next != current)
        {
            _moveWindow(next);
        }
    }

    private PointerStimulus MeasurePointer(
        PixelPoint pointer,
        PixelRect content,
        TimeSpan elapsed)
    {
        var origin = CurrentFrame.State.Position;
        var center = new LocomotionPoint(
            origin.X + content.X + content.Width / 2d,
            origin.Y + content.Y + content.Height / 2d);
        var pointerPoint = new LocomotionPoint(pointer.X, pointer.Y);
        var distance = (pointerPoint - center).Length;
        var speed = _lastPointer is { } previous && elapsed > TimeSpan.Zero
            ? new LocomotionPoint(pointer.X - previous.X, pointer.Y - previous.Y).Length
                / elapsed.TotalSeconds
            : 0;
        _lastPointer = pointer;
        if (distance <= 260 && speed < 480)
        {
            _pointerInterestSeconds = Math.Min(3, _pointerInterestSeconds + elapsed.TotalSeconds);
        }
        else
        {
            _pointerInterestSeconds = Math.Max(0, _pointerInterestSeconds - elapsed.TotalSeconds * 2);
        }
        if (_chaseCooldown > TimeSpan.Zero)
        {
            _chaseCooldown -= elapsed;
        }

        return new PointerStimulus(
            distance,
            speed,
            _pointerInterestSeconds,
            _chaseCooldown);
    }

    private LocomotionPoint SelectTarget(
        LocomotionIntent intent,
        LocomotionPoint origin,
        PixelPoint pointer,
        PixelRect content,
        LocomotionBounds bounds)
    {
        if (intent is LocomotionIntent.Idle or LocomotionIntent.Anchor)
        {
            return origin;
        }
        if (intent == LocomotionIntent.ApproachPointer)
        {
            return bounds.Clamp(new LocomotionPoint(
                pointer.X - content.X - content.Width / 2d,
                pointer.Y - content.Y - content.Height / 2d));
        }
        if (intent == LocomotionIntent.RetreatPointer)
        {
            var center = new LocomotionPoint(
                origin.X + content.X + content.Width / 2d,
                origin.Y + content.Y + content.Height / 2d);
            var away = (center - new LocomotionPoint(pointer.X, pointer.Y)).Normalize();
            if (away == LocomotionPoint.Zero)
            {
                away = new LocomotionPoint(1, 0);
            }
            return bounds.Clamp(origin + away * 180);
        }

        return new LocomotionPoint(
            bounds.Left + _random.NextDouble() * (bounds.Right - bounds.Left),
            bounds.Top + _random.NextDouble() * (bounds.Bottom - bounds.Top));
    }
}
