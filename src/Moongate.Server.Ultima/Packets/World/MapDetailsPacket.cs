using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.MapItems;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells a client of 7.0.13 or newer the area a map item shows (0xF5): the map gump, the corners, the size of the
///     drawing and the facet.
/// </summary>
[PacketHandler(0xF5, PacketSizing.Fixed, Length = 21)]
public sealed class MapDetailsPacket : BaseFixedPacket<MapDetailsPacket>, IOutgoingPacket
{
    /// <summary>
    ///     The gump the client draws a map in.
    /// </summary>
    public const ushort MapGump = 0x139D;

    public uint Serial { get; }

    public MapArea Area { get; }

    public MapDetailsPacket(uint serial, MapArea area)
    {
        Serial = serial;
        Area = area;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt32BigEndian(Serial);
        writer.WriteUInt16BigEndian(MapGump);
        writer.WriteUInt16BigEndian((ushort)Area.X1);
        writer.WriteUInt16BigEndian((ushort)Area.Y1);
        writer.WriteUInt16BigEndian((ushort)Area.X2);
        writer.WriteUInt16BigEndian((ushort)Area.Y2);
        writer.WriteUInt16BigEndian((ushort)Area.Width);
        writer.WriteUInt16BigEndian((ushort)Area.Height);
        writer.WriteUInt16BigEndian((ushort)Area.Facet);
    }
}
