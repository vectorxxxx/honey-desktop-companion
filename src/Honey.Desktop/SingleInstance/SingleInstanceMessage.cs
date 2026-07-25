using System.Text;
using System.IO;

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
    public const int MaximumRequestFrameBytes = 64;
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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

    public static async Task<SingleInstanceRequest?> ReadRequestFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new byte[MaximumRequestFrameBytes + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(count, 1),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }
            if (buffer[count++] == (byte)'\n')
            {
                try
                {
                    var frameLength =
                        count >= 2 && buffer[count - 2] == (byte)'\r'
                            ? count - 2
                            : count - 1;
                    var frame = StrictUtf8.GetString(buffer, 0, frameLength);
                    return TryParseRequest(frame, out var request) ? request : null;
                }
                catch (DecoderFallbackException)
                {
                    return null;
                }
            }
        }
        return null;
    }

    private static string CommandText(SingleInstanceCommand command) =>
        command switch
        {
            SingleInstanceCommand.Show => "show",
            SingleInstanceCommand.Shutdown => "shutdown",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
}

public sealed class SingleInstanceRequestCache
{
    private sealed record Entry(
        SingleInstanceCommand Command,
        Lazy<Task<bool>> Execution)
    {
        public DateTimeOffset? CompletedAt { get; set; }
    }

    private readonly object _sync = new();
    private readonly Dictionary<Guid, Entry> _entries = [];
    private readonly int _completedCapacity;
    private readonly TimeSpan _completedTtl;
    private readonly Func<DateTimeOffset> _utcNow;

    public SingleInstanceRequestCache(
        int completedCapacity = 256,
        TimeSpan? completedTtl = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (completedCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(completedCapacity));
        }
        _completedCapacity = completedCapacity;
        _completedTtl = completedTtl ?? TimeSpan.FromMinutes(2);
        if (_completedTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(completedTtl));
        }
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public int Count
    {
        get { lock (_sync) return _entries.Count; }
    }

    public bool TryGetOrAdd(
        SingleInstanceRequest request,
        Func<Task<bool>> execute,
        out Task<bool> completion)
    {
        ArgumentNullException.ThrowIfNull(execute);
        Entry entry;
        lock (_sync)
        {
            PruneCompleted(_utcNow());
            if (_entries.TryGetValue(request.RequestId, out entry!))
            {
                if (entry.Command != request.Command)
                {
                    completion = Task.FromResult(false);
                    return false;
                }
            }
            else
            {
                entry = new Entry(
                    request.Command,
                    new Lazy<Task<bool>>(
                        () => ExecuteAndMarkAsync(request.RequestId, execute),
                        LazyThreadSafetyMode.ExecutionAndPublication));
                _entries.Add(request.RequestId, entry);
            }
            completion = entry.Execution.Value;
        }
        return true;
    }

    public void MarkCompleted(Guid requestId)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(requestId, out var entry) &&
                entry.Execution.IsValueCreated &&
                entry.Execution.Value.IsCompleted)
            {
                entry.CompletedAt ??= _utcNow();
            }
            PruneCompleted(_utcNow());
        }
    }

    private async Task<bool> ExecuteAndMarkAsync(Guid requestId, Func<Task<bool>> execute)
    {
        try
        {
            return await execute().ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                if (_entries.TryGetValue(requestId, out var entry))
                {
                    entry.CompletedAt ??= _utcNow();
                }
                PruneCompleted(_utcNow());
            }
        }
    }

    private void PruneCompleted(DateTimeOffset now)
    {
        foreach (var expired in _entries
            .Where(pair =>
                pair.Value.CompletedAt is { } completedAt &&
                now - completedAt >= _completedTtl)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _entries.Remove(expired);
        }

        var overflow = _entries.Count(pair => pair.Value.CompletedAt is not null)
            - _completedCapacity;
        if (overflow <= 0) return;
        foreach (var key in _entries
            .Where(pair => pair.Value.CompletedAt is not null)
            .OrderBy(pair => pair.Value.CompletedAt)
            .Take(overflow)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _entries.Remove(key);
        }
    }
}
