using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.MapItems;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells a client older than 7.0.13 the area a map item shows (0x90): as <see cref="MapDetailsPacket" /> without the
///     facet, so only maps of Felucca and Trammel.
/// </summary>
[PacketHandler(0x90, PacketSizing.Fixed, Length = 19)]
public sealed class OldMapDetailsPacket : BaseFixedPacket<OldMapDetailsPacket>, IOutgoingPacket
{
    public uint Serial { get; }

    public MapArea Area { get; }

    public OldMapDetailsPacket(uint serial, MapArea area)
    {
        Serial = serial;
        Area = area;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt32BigEndian(Serial);
        writer.WriteUInt16BigEndian(MapDetailsPacket.MapGump);
        writer.WriteUInt16BigEndian((ushort)Area.X1);
        writer.WriteUInt16BigEndian((ushort)Area.Y1);
        writer.WriteUInt16BigEndian((ushort)Area.X2);
        writer.WriteUInt16BigEndian((ushort)Area.Y2);
        writer.WriteUInt16BigEndian((ushort)Area.Width);
        writer.WriteUInt16BigEndian((ushort)Area.Height);
    }
}
