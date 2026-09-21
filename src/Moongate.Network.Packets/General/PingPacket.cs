using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.General;

[PacketHandler(0x73, PacketSizing.Fixed, Length = 2)]
public sealed class PingPacket : BaseFixedPacket<PingPacket>, IIncomingPacket<PingPacket>, IOutgoingPacket
{
    public byte Sequence { get; }

    public PingPacket(byte sequence)
    {
        Sequence = sequence;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out PingPacket? packet)
    {
        packet = null;
        if (!HasValidHeader(data))
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
