using Honey.Integrations.Windows;

namespace Honey.Desktop.Interaction;

public sealed class PointerInteractionFinalizer
{
    private readonly PetInteractionController _controller;

    public PointerInteractionFinalizer(PetInteractionController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public bool Complete(PixelPoint pointer)
    {
        var wasDragging = _controller.IsDragging;
        _controller.End(pointer);
        return wasDragging;
    }

    public bool Cancel() => _controller.Cancel();
}
