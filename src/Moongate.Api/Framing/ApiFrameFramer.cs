using System.Buffers.Binary;
using Moongate.Api.Exceptions;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Api.Framing;

/// <summary>Extracts frames prefixed by their four-byte big-endian payload length.</summary>
public sealed class ApiFrameFramer : INetFramer
{
    private readonly int _maxFrameLength;

    /// <summary>Creates a framer with the maximum payload length, excluding its prefix.</summary>
    public ApiFrameFramer(int maxFrameLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxFrameLength, 16 * 1024 * 1024 - sizeof(uint));
        _maxFrameLength = maxFrameLength;
    }

    /// <inheritdoc />
    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (buffer.Length < sizeof(uint)) { return false; }
        var declared = BinaryPrimitives.ReadUInt32BigEndian(buffer);

        if (declared == 0 || declared > _maxFrameLength)
        {
            throw new ApiProtocolException("Invalid frame length.");
        }
        frameLength = checked((int)declared + sizeof(uint));

        return buffer.Length >= frameLength;
    }
}
