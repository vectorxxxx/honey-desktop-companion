using Honey.Domain.Events;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Interaction;

public sealed class PetInteractionController
{
    private readonly Guid _petId;
    private readonly Action<PetInteractionOccurred> _interactionOccurred;
    private readonly Action<PixelPoint> _moveWindow;
    private readonly Action<bool> _autonomousMovementPaused;
    private readonly Action<Exception>? _errorSink;
    private readonly int _dragThresholdPixels;
    private PixelPoint _pointerOrigin;
    private PixelPoint _windowOrigin;
    private PixelPoint? _lastWindowPosition;
    private bool _pressed;

    public PetInteractionController(
        Guid petId,
        Action<PetInteractionOccurred> interactionOccurred,
        Action<PixelPoint> moveWindow,
        Action<bool>? autonomousMovementPaused = null,
        Action<Exception>? errorSink = null,
        int dragThresholdPixels = 6)
    {
        if (petId == Guid.Empty)
        {
            throw new ArgumentException("宠物标识不能为空。", nameof(petId));
        }

        _petId = petId;
        _interactionOccurred = interactionOccurred ?? throw new ArgumentNullException(nameof(interactionOccurred));
        _moveWindow = moveWindow ?? throw new ArgumentNullException(nameof(moveWindow));
        _autonomousMovementPaused = autonomousMovementPaused ?? (_ => { });
        _errorSink = errorSink;
        _dragThresholdPixels = Math.Max(1, dragThresholdPixels);
    }

    public bool IsDragging { get; private set; }

    public void Begin(PixelPoint pointerScreen, PixelPoint windowOrigin)
    {
        if (_pressed || IsDragging)
        {
            Cancel();
        }

        _pointerOrigin = pointerScreen;
        _windowOrigin = windowOrigin;
        _pressed = true;
        IsDragging = false;
        _lastWindowPosition = null;
    }

    public void Move(PixelPoint pointerScreen)
    {
        if (!_pressed)
        {
            return;
        }

        var deltaX = pointerScreen.X - _pointerOrigin.X;
        var deltaY = pointerScreen.Y - _pointerOrigin.Y;
        if (!IsDragging
            && (Math.Abs(deltaX) > _dragThresholdPixels || Math.Abs(deltaY) > _dragThresholdPixels))
        {
            IsDragging = true;
            SafeCallback.Invoke(() => _autonomousMovementPaused(true), _errorSink);
        }

        if (IsDragging)
        {
            var next = new PixelPoint(_windowOrigin.X + deltaX, _windowOrigin.Y + deltaY);
            if (_lastWindowPosition != next)
            {
                _lastWindowPosition = next;
                SafeCallback.Invoke(() => _moveWindow(next), _errorSink);
            }
        }
    }

    public void End(PixelPoint pointerScreen)
    {
        if (!_pressed)
        {
            return;
        }

        var wasDragging = IsDragging;
        try
        {
            Move(pointerScreen);
            wasDragging = IsDragging;
            if (!wasDragging)
            {
                SafeCallback.Invoke(
                    () => _interactionOccurred(new PetInteractionOccurred(_petId, "pet")),
                    _errorSink);
            }
        }
        finally
        {
            _pressed = false;
            var releasePause = IsDragging;
            IsDragging = false;
            if (releasePause)
            {
                SafeCallback.Invoke(() => _autonomousMovementPaused(false), _errorSink);
            }
        }
    }

    public bool Cancel()
    {
        _pressed = false;
        var wasDragging = IsDragging;
        IsDragging = false;
        if (wasDragging)
        {
            SafeCallback.Invoke(() => _autonomousMovementPaused(false), _errorSink);
        }

        return wasDragging;
    }
}
