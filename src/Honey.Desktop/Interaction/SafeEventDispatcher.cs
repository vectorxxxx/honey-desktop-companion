namespace Honey.Desktop.Interaction;

public static class SafeEventDispatcher
{
    public static void Publish<T>(
        Action<T>? handlers,
        T value,
        Action<Exception>? errorSink = null)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList().Cast<Action<T>>())
        {
            SafeCallback.Invoke(() => subscriber(value), errorSink);
        }
    }
}
