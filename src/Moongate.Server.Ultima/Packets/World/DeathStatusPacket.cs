using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells the client of a player that its character is dead (0x2C), as ModernUO does: the client shows the death
///     screen and takes the ghost view.
/// </summary>
[PacketHandler(0x2C, PacketSizing.Fixed, Length = 2)]
public sealed class DeathStatusPacket : BaseFixedPacket<DeathStatusPacket>, IOutgoingPacket
{
    private const byte DeadStatus = 2;

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(DeadStatus);
    }
}
