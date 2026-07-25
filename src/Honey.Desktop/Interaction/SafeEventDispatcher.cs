namespace Honey.Desktop.Interaction;

public static class SafeEventDispatcher
{
    public static void Publish(
        Action<bool>? handlers,
        bool value,
        Action<Exception>? errorSink = null)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList().Cast<Action<bool>>())
        {
            SafeCallback.Invoke(() => subscriber(value), errorSink);
        }
    }
}
