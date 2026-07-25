using System.Text;

namespace Honey.Desktop.SingleInstance;

public enum SingleInstanceCommand
{
    Show
}

public static class SingleInstanceMessage
{
    private static readonly byte[] ShowBytes = Encoding.UTF8.GetBytes("show");

    public static bool TryParse(ReadOnlySpan<byte> message, out SingleInstanceCommand command)
    {
        if (message.SequenceEqual(ShowBytes))
        {
            command = SingleInstanceCommand.Show;
            return true;
        }

        command = default;
        return false;
    }
}
