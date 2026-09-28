using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Draws the player's own mobile: body, hue, state, location and facing (0x20, 19 bytes).
/// </summary>
[PacketHandler(0x20, PacketSizing.Fixed, Length = 19)]
public sealed class MobileUpdatePacket : BaseFixedPacket<MobileUpdatePacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public Body Body { get; }

    public Hue Hue { get; }

    public MobileFlagsType Flags { get; }

    public Point3D Location { get; }

    public DirectionType Direction { get; }

    public MobileUpdatePacket(
        Serial serial,
        Body body,
        Hue hue,
        MobileFlagsType flags,
        Point3D location,
        DirectionType direction
    )
    {
        Serial = serial;
        Body = body;
        Hue = hue;
        Flags = flags;
        Location = location;
        Direction = direction;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian(Body.Value);
        writer.WriteByte(0);
        writer.WriteUInt16BigEndian(Hue.Value);
        writer.WriteByte((byte)Flags);
        writer.WriteUInt16BigEndian((ushort)Location.X);
        writer.WriteUInt16BigEndian((ushort)Location.Y);
        writer.WriteUInt16BigEndian(0);
        writer.WriteByte((byte)Direction);
        writer.WriteByte(unchecked((byte)(sbyte)Location.Z));
    }
}
