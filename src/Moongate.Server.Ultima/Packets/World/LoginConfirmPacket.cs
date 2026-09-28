using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Primitives;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells the client which mobile it plays and where it stands, with the size of the map (0x1B, 37 bytes). The first
///     packet of entering the world.
/// </summary>
[PacketHandler(0x1B, PacketSizing.Fixed, Length = 37)]
public sealed class LoginConfirmPacket : BaseFixedPacket<LoginConfirmPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public Body Body { get; }

    public Point3D Location { get; }

    public DirectionType Direction { get; }

    public int MapWidth { get; }

    public int MapHeight { get; }

    public LoginConfirmPacket(Serial serial, Body body, Point3D location, DirectionType direction, int mapWidth, int mapHeight)
    {
        Serial = serial;
        Body = body;
        Location = location;
        Direction = direction;
        MapWidth = mapWidth;
        MapHeight = mapHeight;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt32BigEndian(0);
        writer.WriteUInt16BigEndian(Body.Value);
        writer.WriteUInt16BigEndian((ushort)Location.X);
        writer.WriteUInt16BigEndian((ushort)Location.Y);
        writer.WriteUInt16BigEndian(unchecked((ushort)(short)Location.Z));
        writer.WriteByte((byte)Direction);
        writer.WriteByte(0);
        writer.WriteUInt32BigEndian(uint.MaxValue);
        writer.WriteUInt32BigEndian(0);
        writer.WriteUInt16BigEndian((ushort)MapWidth);
        writer.WriteUInt16BigEndian((ushort)MapHeight);

        // The rest of the 37 bytes is zero.
        writer.WriteUInt32BigEndian(0);
        writer.WriteUInt16BigEndian(0);
    }
}
