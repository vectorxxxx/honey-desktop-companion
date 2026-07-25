using Honey.Domain.Events;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Interaction;

public sealed class PetInteractionController
{
    private readonly Guid _petId;
    private readonly Action<PetInteractionOccurred> _interactionOccurred;
    private readonly Action<PixelPoint> _moveWindow;
    private readonly Action<bool> _autonomousMovementPaused;
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
        _dragThresholdPixels = Math.Max(1, dragThresholdPixels);
    }

    public bool IsDragging { get; private set; }

    public void Begin(PixelPoint pointerScreen, PixelPoint windowOrigin)
    {
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
            _autonomousMovementPaused(true);
        }

        if (IsDragging)
        {
            var next = new PixelPoint(_windowOrigin.X + deltaX, _windowOrigin.Y + deltaY);
            if (_lastWindowPosition != next)
            {
                _lastWindowPosition = next;
                _moveWindow(next);
            }
        }
    }

    public void End(PixelPoint pointerScreen)
    {
        if (!_pressed)
        {
            return;
        }

        Move(pointerScreen);
        _pressed = false;
        if (IsDragging)
        {
            IsDragging = false;
            _autonomousMovementPaused(false);
            return;
        }

        _interactionOccurred(new PetInteractionOccurred(_petId, "pet"));
    }

    public void Cancel()
    {
        _pressed = false;
        if (IsDragging)
        {
            IsDragging = false;
            _autonomousMovementPaused(false);
        }
    }
}
