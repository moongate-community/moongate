using System.Buffers.Binary;

using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Tests.Support;

public sealed class FourByteLengthPrefixFramer : INetFramer
{
    public FourByteLengthPrefixFramer()
    {
    }

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (buffer.Length < 4)
        {
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(buffer);
        var total = 4 + payloadLength;

        if (buffer.Length < total)
        {
            return false;
        }

        frameLength = total;

        return true;
    }
}
