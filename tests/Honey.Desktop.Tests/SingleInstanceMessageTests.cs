using System.Text;
using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

public sealed class SingleInstanceMessageTests
{
    [Fact]
    public void TryParse_只接受严格的Show命令()
    {
        var accepted = SingleInstanceMessage.TryParse(Encoding.UTF8.GetBytes("show"), out var command);

        Assert.True(accepted);
        Assert.Equal(SingleInstanceCommand.Show, command);
    }

    [Fact]
    public void TryParse_接受严格的Shutdown命令()
    {
        var accepted = SingleInstanceMessage.TryParse(
            Encoding.UTF8.GetBytes("shutdown"),
            out var command);

        Assert.True(accepted);
        Assert.Equal(SingleInstanceCommand.Shutdown, command);
    }

    [Theory]
    [InlineData("SHOW")]
    [InlineData("show\n")]
    [InlineData(" show")]
    [InlineData("hide")]
    [InlineData("")]
    public void TryParse_拒绝非协议消息(string message)
    {
        var accepted = SingleInstanceMessage.TryParse(Encoding.UTF8.GetBytes(message), out _);

        Assert.False(accepted);
    }

    [Fact]
    public async Task ReadRequestFrameAsync_拒绝超长截断无换行和非法Utf8()
    {
        var valid = Encoding.UTF8.GetBytes($"{Guid.NewGuid():N}|show\n");
        var parsed = await SingleInstanceMessage.ReadRequestFrameAsync(
            new MemoryStream(valid),
            TestContext.Current.CancellationToken);
        Assert.Equal(SingleInstanceCommand.Show, parsed?.Command);

        foreach (var invalid in new[]
        {
            Enumerable.Repeat((byte)'a', 4096).ToArray(),
            Encoding.UTF8.GetBytes($"{Guid.NewGuid():N}|show"),
            new byte[] { 0xff, 0xfe, (byte)'\n' }
        })
        {
            var stream = new CountingReadStream(invalid);
            Assert.Null(await SingleInstanceMessage.ReadRequestFrameAsync(
                stream,
                TestContext.Current.CancellationToken));
            Assert.True(
                stream.BytesRead <= SingleInstanceMessage.MaximumRequestFrameBytes + 1);
        }
    }

    [Fact]
    public async Task RequestCache_绑定命令并按Ttl和容量淘汰已完成结果()
    {
        var now = DateTimeOffset.UtcNow;
        var cache = new SingleInstanceRequestCache(
            completedCapacity: 2,
            completedTtl: TimeSpan.FromMinutes(2),
            utcNow: () => now);
        var id = Guid.NewGuid();
        Assert.True(cache.TryGetOrAdd(
            new SingleInstanceRequest(id, SingleInstanceCommand.Show),
            () => Task.FromResult(true),
            out var first));
        Assert.True(await first);
        cache.MarkCompleted(id);
        Assert.False(cache.TryGetOrAdd(
            new SingleInstanceRequest(id, SingleInstanceCommand.Shutdown),
            () => Task.FromResult(true),
            out _));

        for (var index = 0; index < 10_000; index++)
        {
            var request = new SingleInstanceRequest(Guid.NewGuid(), SingleInstanceCommand.Show);
            Assert.True(cache.TryGetOrAdd(
                request,
                () => Task.FromResult(true),
                out var completion));
            Assert.True(await completion);
            cache.MarkCompleted(request.RequestId);
        }
        Assert.InRange(cache.Count, 1, 2);

        now += TimeSpan.FromMinutes(3);
        var finalRequest = new SingleInstanceRequest(Guid.NewGuid(), SingleInstanceCommand.Show);
        Assert.True(cache.TryGetOrAdd(
            finalRequest,
            () => Task.FromResult(true),
            out var final));
        Assert.True(await final);
        cache.MarkCompleted(finalRequest.RequestId);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task RequestCache_不会淘汰仍在处理的请求()
    {
        var cache = new SingleInstanceRequestCache(completedCapacity: 1);
        var pendingSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new SingleInstanceRequest(Guid.NewGuid(), SingleInstanceCommand.Show);
        Assert.True(cache.TryGetOrAdd(
            pending,
            () => pendingSource.Task,
            out var pendingCompletion));
        for (var index = 0; index < 20; index++)
        {
            var request = new SingleInstanceRequest(Guid.NewGuid(), SingleInstanceCommand.Show);
            Assert.True(cache.TryGetOrAdd(
                request,
                () => Task.FromResult(true),
                out var completed));
            Assert.True(await completed);
            cache.MarkCompleted(request.RequestId);
        }
        Assert.Equal(2, cache.Count);
        pendingSource.SetResult(true);
        Assert.True(await pendingCompletion);
        Assert.Equal(1, cache.Count);
    }

    private sealed class CountingReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public int BytesRead { get; private set; }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = _inner.ReadAsync(buffer, cancellationToken);
            if (read.IsCompletedSuccessfully)
            {
                BytesRead += read.Result;
                return read;
            }
            return TrackAsync(read);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private async ValueTask<int> TrackAsync(ValueTask<int> read)
        {
            var result = await read;
            BytesRead += result;
            return result;
        }
    }
}
