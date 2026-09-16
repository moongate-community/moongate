using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Tests.Support;

public sealed class PendingFrameBufferTestFramer : INetFramer
{
    private readonly Exception? _exception;
    private readonly int? _reportedLength;

    public PendingFrameBufferTestFramer(int? reportedLength = null, Exception? exception = null)
    {
        _reportedLength = reportedLength;
        _exception = exception;
    }

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        if (_exception is not null)
        {
            throw _exception;
        }

        if (_reportedLength.HasValue)
        {
            frameLength = _reportedLength.Value;
            return true;
        }

        if (buffer.IsEmpty)
        {
            frameLength = 0;
            return false;
        }

        frameLength = buffer[0] + 1;
        return buffer.Length >= frameLength;
    }
}
