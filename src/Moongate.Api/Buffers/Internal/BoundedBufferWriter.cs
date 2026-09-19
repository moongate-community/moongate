using System.Buffers;
using Moongate.Api.Exceptions;
namespace Moongate.Api.Buffers.Internal;
internal sealed class BoundedBufferWriter : IBufferWriter<byte>
{
    private readonly int _limit;
    private readonly int _scratchLimit;
    private byte[] _buffer = [];
    private int _written;
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);
    public BoundedBufferWriter(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        _limit = limit;
        _scratchLimit = checked(limit * 3 + 32);
    }
    public void Advance(int count)
    {
        if (count < 0 || count > _limit - _written || count > _buffer.Length - _written)
        { throw new ApiProtocolException("Payload limit exceeded."); }
        _written += count;
    }
    public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return _buffer.AsMemory(_written); }
    public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return _buffer.AsSpan(_written); }
    private void Ensure(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        sizeHint = Math.Max(1, sizeHint);
        if (sizeHint > _scratchLimit - _written) { throw new ApiProtocolException("Payload reservation limit exceeded."); }
        var required = _written + sizeHint;
        if (required > _buffer.Length)
        {
            Array.Resize(ref _buffer, Math.Min(_scratchLimit, Math.Max(required, Math.Max(256, _buffer.Length * 2))));
        }
    }
}
