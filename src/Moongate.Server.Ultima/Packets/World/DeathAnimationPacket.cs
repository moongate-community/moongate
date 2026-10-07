using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Makes the client play the death of a mobile (0xAF) and tie it to its corpse: the client picks the animation of
///     the body itself. Written as ModernUO does, with the last four bytes at zero.
/// </summary>
[PacketHandler(0xAF, PacketSizing.Fixed, Length = 13)]
public sealed class DeathAnimationPacket : BaseFixedPacket<DeathAnimationPacket>, IOutgoingPacket
{
    public Serial Mobile { get; }

    /// <summary>
    ///     Gets the serial of the corpse, or zero when the mobile leaves none.
    /// </summary>
    public Serial Corpse { get; }

    public DeathAnimationPacket(Serial mobile, Serial corpse)
    {
        Mobile = mobile;
        Corpse = corpse;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt32BigEndian(Mobile.Value);
        writer.WriteUInt32BigEndian(Corpse.Value);
        writer.WriteUInt32BigEndian(0);
    }
}
