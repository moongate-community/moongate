using Moongate.Api.Hosting.Internal;

namespace Moongate.Api.Streams.Internal;

/// <summary>Releases unsuccessful setup admission when the transport disposes its owned stream.</summary>
internal sealed class ApiAdmissionStream : Stream
{
    private readonly Stream _inner;
    private readonly ApiConnectionAdmission _admission;
    private int _disposed;
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public ApiAdmissionStream(Stream inner, ApiConnectionAdmission admission)
    {
        _inner = inner;
        _admission = admission;
    }

    public override void Flush()
        => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer)
        => _inner.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin)
        => _inner.Seek(offset, origin);

    public override void SetLength(long value)
        => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => _inner.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer)
        => _inner.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);

    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { await _inner.DisposeAsync().ConfigureAwait(false); }
                finally { _admission.ReleaseTransport(); }
            }
        }
        finally
        {
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { _inner.Dispose(); }
                finally { _admission.ReleaseTransport(); }
            }
        }
        finally { base.Dispose(disposing); }
    }
}
