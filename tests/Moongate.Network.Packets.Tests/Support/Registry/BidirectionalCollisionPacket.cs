using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Registry;

[PacketHandler(0xE1, PacketSizing.Fixed, Length = 1)]
public sealed class BidirectionalCollisionPacket
    : BaseFixedPacket<BidirectionalCollisionPacket>, IIncomingPacket<BidirectionalCollisionPacket>, IOutgoingPacket
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out BidirectionalCollisionPacket? packet)
    {
        packet = data.SequenceEqual(new byte[] { 0xE1 }) ? new BidirectionalCollisionPacket() : null;

        return packet is not null;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.WriteByte(OpCode);
    }
}
