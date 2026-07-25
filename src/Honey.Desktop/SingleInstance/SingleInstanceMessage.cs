using System.Text;

namespace Honey.Desktop.SingleInstance;

public enum SingleInstanceCommand
{
    Show,
    Shutdown
}

public static class SingleInstanceMessage
{
    private static readonly byte[] ShowBytes = Encoding.UTF8.GetBytes("show");
    private static readonly byte[] ShutdownBytes = Encoding.UTF8.GetBytes("shutdown");
    private static readonly byte[] ShowFrame = Encoding.UTF8.GetBytes("show\n");
    private static readonly byte[] ShutdownFrame = Encoding.UTF8.GetBytes("shutdown\n");

    public static bool TryParse(ReadOnlySpan<byte> message, out SingleInstanceCommand command)
    {
        if (message.SequenceEqual(ShowBytes))
        {
            command = SingleInstanceCommand.Show;
            return true;
        }

        if (message.SequenceEqual(ShutdownBytes))
        {
            command = SingleInstanceCommand.Shutdown;
            return true;
        }

        command = default;
        return false;
    }

    public static ReadOnlyMemory<byte> Serialize(SingleInstanceCommand command) =>
        command switch
        {
            SingleInstanceCommand.Show => ShowFrame,
            SingleInstanceCommand.Shutdown => ShutdownFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
}
