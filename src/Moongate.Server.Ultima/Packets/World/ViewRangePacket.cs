using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells the client how far it sees (0xC8): the server's view range, as ModernUO answers the client's own 0xC8.
/// </summary>
[PacketHandler(0xC8, PacketSizing.Fixed, Length = 2)]
public sealed class ViewRangePacket : BaseFixedPacket<ViewRangePacket>, IOutgoingPacket
{
    public byte Range { get; }

    public ViewRangePacket(int range)
    {
        Range = (byte)range;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(Range);
    }
}
