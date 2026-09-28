using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Refuses the step with the sequence (0x21) and puts the client back at the mover's real position.
/// </summary>
[PacketHandler(0x21, PacketSizing.Fixed, Length = 8)]
public sealed class MovementRejectPacket : BaseFixedPacket<MovementRejectPacket>, IOutgoingPacket
{
    public byte Sequence { get; }

    public Point3D Location { get; }

    public DirectionType Direction { get; }

    public MovementRejectPacket(byte sequence, Point3D location, DirectionType direction)
    {
        Sequence = sequence;
        Location = location;
        Direction = direction;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(Sequence);
        writer.WriteUInt16BigEndian((ushort)Location.X);
        writer.WriteUInt16BigEndian((ushort)Location.Y);
        writer.WriteByte((byte)Direction);
        writer.WriteByte(unchecked((byte)(sbyte)Location.Z));
    }
}
