using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Tests.Support;

public sealed class StatefulPendingFrameFramer : INetFramer
{
    private byte _headerKey = 0x5A;
    private int? _pendingFrameLength;

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        if (!_pendingFrameLength.HasValue)
        {
            if (buffer.IsEmpty)
            {
                frameLength = 0;

                return false;
            }

            buffer[0] ^= _headerKey;
            _pendingFrameLength = buffer[0];
        }

        if (buffer.Length < _pendingFrameLength.Value)
        {
            frameLength = 0;

            return false;
        }

        frameLength = _pendingFrameLength.Value;
        _pendingFrameLength = null;
        _headerKey++;

        return true;
    }
}
