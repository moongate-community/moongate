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
///     Moves or turns a mobile the client already shows (0x77); the high bit of the direction marks a run.
/// </summary>
[PacketHandler(0x77, PacketSizing.Fixed, Length = 17)]
public sealed class MobileMovingPacket : BaseFixedPacket<MobileMovingPacket>, IOutgoingPacket
{
    private const byte RunningBit = 0x80;

    public Serial Serial { get; }

    public Body Body { get; }

    public Point3D Location { get; }

    public DirectionType Direction { get; }

    public bool Running { get; }

    public Hue Hue { get; }

    public MobileFlagsType Flags { get; }

    public NotorietyType Notoriety { get; }

    public MobileMovingPacket(
        Serial serial,
        Body body,
        Point3D location,
        DirectionType direction,
        bool running,
        Hue hue,
        MobileFlagsType flags,
        NotorietyType notoriety
    )
    {
        Serial = serial;
        Body = body;
        Location = location;
        Direction = direction;
        Running = running;
        Hue = hue;
        Flags = flags;
        Notoriety = notoriety;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian(Body.Value);
        writer.WriteUInt16BigEndian((ushort)Location.X);
        writer.WriteUInt16BigEndian((ushort)Location.Y);
        writer.WriteByte(unchecked((byte)(sbyte)Location.Z));
        writer.WriteByte((byte)((byte)Direction | (Running ? RunningBit : 0)));
        writer.WriteUInt16BigEndian(Hue.Value);
        writer.WriteByte((byte)Flags);
        writer.WriteByte((byte)Notoriety);
    }
}
