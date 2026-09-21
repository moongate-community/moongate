using System.Buffers;

namespace Moongate.Network.Packets.Spans;

/// <summary>
/// Owns the pooled buffer transferred by <see cref="SpanWriter.ToSpan" />.
/// Spans obtained from this owner must not be used after disposal.
/// </summary>
public sealed class SpanOwner : IDisposable
{
    private readonly int _length;
    private byte[]? _buffer;

    public Span<byte> Span => _buffer is { } buffer ? buffer.AsSpan(0, _length) : Span<byte>.Empty;

    internal SpanOwner(int length, byte[]? buffer)
    {
        _length = length;
        _buffer = buffer;
    }

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);

        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
        }
    }
}
