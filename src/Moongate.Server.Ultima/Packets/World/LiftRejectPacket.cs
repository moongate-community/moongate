using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Items;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Refuses to let the player pick an item up (0x27), with the reason the client shows.
/// </summary>
[PacketHandler(0x27, PacketSizing.Fixed, Length = 2)]
public sealed class LiftRejectPacket : BaseFixedPacket<LiftRejectPacket>, IOutgoingPacket
{
    public LiftRejectReasonType Reason { get; }

    public LiftRejectPacket(LiftRejectReasonType reason)
    {
        Reason = reason;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Reason);
    }
}
