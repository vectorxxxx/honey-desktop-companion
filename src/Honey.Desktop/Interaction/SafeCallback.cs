namespace Honey.Desktop.Interaction;

internal static class SafeCallback
{
    public static void Invoke(Action callback, Action<Exception>? errorSink)
    {
        try
        {
            callback();
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            Report(exception, errorSink);
        }
    }

    private static void Report(Exception exception, Action<Exception>? errorSink)
    {
        if (errorSink is null)
        {
            return;
        }

        try
        {
            errorSink(exception);
        }
        catch (Exception sinkException) when (!IsFatal(sinkException))
        {
            // 错误报告器本身的普通故障不能反向击穿输入事件。
        }
    }

    private static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException;
}
