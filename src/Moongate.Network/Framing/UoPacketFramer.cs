using System.Buffers.Binary;
using Moongate.Network.Exceptions;
using Moongate.Network.Protocol;
using SquidStd.Network.Interfaces.Framing;

namespace Moongate.Network.Framing;

/// <summary>
/// Cuts UO packets out of the accumulated TCP stream: byte 0 is the packet id,
/// the size comes from <see cref="PacketLengths"/> (fixed) or from the big-endian
/// ushort at bytes 1-2 (variable). Unknown ids and impossible lengths throw
/// <see cref="UoFramingException"/> — the session layer disconnects the client.
/// Stateless: safe to call repeatedly while the buffer grows.
/// </summary>
public sealed class UoPacketFramer : INetFramer
{
    private const int VariableHeaderSize = 3;

    public bool TryReadFrame(ReadOnlySpan<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (buffer.IsEmpty)
        {
            return false;
        }

        var packetId = buffer[0];
        var declared = PacketLengths.Get(packetId);

        if (declared == PacketLengths.Unknown)
        {
            throw new UoFramingException($"Unknown packet id 0x{packetId:X2}.");
        }

        if (declared != PacketLengths.Variable)
        {
            if (buffer.Length < declared)
            {
                return false;
            }

            frameLength = declared;

            return true;
        }

        if (buffer.Length < VariableHeaderSize)
        {
            return false;
        }

        int total = BinaryPrimitives.ReadUInt16BigEndian(buffer[1..]);

        if (total < VariableHeaderSize)
        {
            throw new UoFramingException($"Packet 0x{packetId:X2} declares impossible length {total}.");
        }

        if (buffer.Length < total)
        {
            return false;
        }

        frameLength = total;

        return true;
    }
}
