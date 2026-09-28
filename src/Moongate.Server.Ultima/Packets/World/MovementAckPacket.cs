using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Accepts the step with the sequence (0x22), with the mover's notoriety.
/// </summary>
[PacketHandler(0x22, PacketSizing.Fixed, Length = 3)]
public sealed class MovementAckPacket : BaseFixedPacket<MovementAckPacket>, IOutgoingPacket
{
    public byte Sequence { get; }

    public NotorietyType Notoriety { get; }

    public MovementAckPacket(byte sequence, NotorietyType notoriety)
    {
        Sequence = sequence;
        Notoriety = notoriety;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(Sequence);
        writer.WriteByte((byte)Notoriety);
    }
}
