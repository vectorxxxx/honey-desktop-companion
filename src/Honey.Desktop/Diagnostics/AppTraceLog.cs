using System.Diagnostics;
using System.IO;
using System.Text;

namespace Honey.Desktop.Diagnostics;

public static class AppTraceLog
{
    private const long DefaultMaximumBytes = 5 * 1024 * 1024;

    public static TextWriterTraceListener Create(
        string logsDirectory,
        long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        if (maximumBytes < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }
        Directory.CreateDirectory(logsDirectory);
        return new TextWriterTraceListener(
            new BoundedRotatingLogWriter(logsDirectory, maximumBytes),
            "HoneyFile");
    }

    private sealed class BoundedRotatingLogWriter : TextWriter
    {
        private static readonly UTF8Encoding Utf8 = new(false);
        private readonly object _sync = new();
        private readonly string _logPath;
        private readonly string _rotatedPath;
        private readonly long _maximumBytes;
        private FileStream _stream;
        private bool _disposed;

        public BoundedRotatingLogWriter(string directory, long maximumBytes)
        {
            _logPath = Path.Combine(directory, "honey.log");
            _rotatedPath = Path.Combine(directory, "honey.log.1");
            _maximumBytes = maximumBytes;
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length >= maximumBytes)
            {
                File.Move(_logPath, _rotatedPath, overwrite: true);
            }
            _stream = OpenAppend();
        }

        public override Encoding Encoding => Utf8;

        public override void Write(char value) => WriteCore(value.ToString());

        public override void Write(string? value)
        {
            if (value is not null) WriteCore(value);
        }

        public override void WriteLine(string? value) =>
            WriteCore((value ?? string.Empty) + Environment.NewLine);

        public override void Flush()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                _stream.Flush(flushToDisk: true);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_sync)
                {
                    if (!_disposed)
                    {
                        _stream.Dispose();
                        _disposed = true;
                    }
                }
            }
            base.Dispose(disposing);
        }

        private void WriteCore(string value)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                var bytes = Utf8.GetBytes(value);
                if (bytes.Length > _maximumBytes)
                {
                    var maximumCharacters =
                        checked((int)Math.Min(int.MaxValue, _maximumBytes / 4));
                    value = value[^maximumCharacters..];
                    bytes = Utf8.GetBytes(value);
                }
                if (_stream.Length + bytes.Length > _maximumBytes)
                {
                    Rotate();
                }
                _stream.Write(bytes);
                _stream.Flush(flushToDisk: false);
            }
        }

        private void Rotate()
        {
            _stream.Dispose();
            File.Move(_logPath, _rotatedPath, overwrite: true);
            _stream = OpenAppend();
        }

        private FileStream OpenAppend() => new(
            _logPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            4096,
            FileOptions.WriteThrough);

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
