using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.General;

public sealed class PingPacket : IIncomingPacket<PingPacket>, IOutgoingPacket
{
    private const byte PacketOpCode = 0x73;
    private const int PacketLength = 2;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
    public byte Sequence { get; }

    public PingPacket(byte sequence)
    {
        Sequence = sequence;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out PingPacket? packet)
    {
        packet = null;
        if (!PacketValidation.HasFixedHeader(data, PacketOpCode, PacketLength))
        {
            return false;
        }

        packet = new PingPacket(data[1]);
        return true;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(Sequence);
    }
}
