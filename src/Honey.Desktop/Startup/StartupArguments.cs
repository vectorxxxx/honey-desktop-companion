namespace Honey.Desktop.Startup;

public enum StartupCommand
{
    Background,
    Show,
    Shutdown,
    VerifyData
}

public sealed record StartupArguments(
    StartupCommand Command,
    string? DataRoot,
    string? InstanceId)
{
    public static StartupArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        StartupCommand? command = null;
        string? dataRoot = null;
        string? instanceId = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            StartupCommand? candidate = argument.ToLowerInvariant() switch
            {
                "--background" => StartupCommand.Background,
                "--show" => StartupCommand.Show,
                "--shutdown" => StartupCommand.Shutdown,
                "--verify-data" => StartupCommand.VerifyData,
                _ => null
            };
            if (candidate is not null)
            {
                if (command is not null)
                {
                    throw new ArgumentException("只能指定一个生命周期命令。", nameof(arguments));
                }

                command = candidate;
                continue;
            }

            if (string.Equals(argument, "--data-root", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index]))
                {
                    throw new ArgumentException("--data-root 必须提供目录。", nameof(arguments));
                }

                dataRoot = arguments[index];
                continue;
            }

            if (string.Equals(argument, "--instance-id", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= arguments.Count
                    || string.IsNullOrWhiteSpace(arguments[index])
                    || arguments[index].Length > 64
                    || arguments[index].Any(character => !char.IsAsciiLetterOrDigit(character)))
                {
                    throw new ArgumentException("--instance-id 必须是 1 至 64 位 ASCII 字母或数字。", nameof(arguments));
                }

                instanceId = arguments[index];
                continue;
            }

            if (string.Equals(argument, "--settings", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            throw new ArgumentException($"无法识别的启动参数：{argument}", nameof(arguments));
        }

        return new StartupArguments(command ?? StartupCommand.Show, dataRoot, instanceId);
    }
}
