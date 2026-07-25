using System.Text;

namespace Honey.Desktop.SingleInstance;

public enum SingleInstanceCommand
{
    Show,
    Shutdown
}

public readonly record struct SingleInstanceRequest(
    Guid RequestId,
    SingleInstanceCommand Command);

public static class SingleInstanceMessage
{
    public static bool TryParse(ReadOnlySpan<byte> message, out SingleInstanceCommand command)
    {
        if (message.SequenceEqual("show"u8))
        {
            command = SingleInstanceCommand.Show;
            return true;
        }
        if (message.SequenceEqual("shutdown"u8))
        {
            command = SingleInstanceCommand.Shutdown;
            return true;
        }
        command = default;
        return false;
    }

    public static bool TryParseRequest(string frame, out SingleInstanceRequest request)
    {
        request = default;
        var separator = frame.IndexOf('|');
        if (separator <= 0 ||
            !Guid.TryParseExact(frame.AsSpan(0, separator), "N", out var requestId) ||
            !TryParse(Encoding.UTF8.GetBytes(frame[(separator + 1)..]), out var command))
        {
            return false;
        }
        request = new SingleInstanceRequest(requestId, command);
        return true;
    }

    public static ReadOnlyMemory<byte> Serialize(SingleInstanceCommand command) =>
        command switch
        {
            SingleInstanceCommand.Show => "show\n"u8.ToArray(),
            SingleInstanceCommand.Shutdown => "shutdown\n"u8.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

    public static string Serialize(SingleInstanceRequest request) =>
        $"{request.RequestId:N}|{CommandText(request.Command)}";

    private static string CommandText(SingleInstanceCommand command) =>
        command switch
        {
            SingleInstanceCommand.Show => "show",
            SingleInstanceCommand.Shutdown => "shutdown",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
}
