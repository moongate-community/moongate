using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Interaction;

[PacketHandler(0x05, PacketSizing.Fixed, Length = 5, Description = "Request Attack")]

/// <summary>
/// Represents RequestAttackPacket.
/// </summary>
public class RequestAttackPacket : BaseGameNetworkPacket
{
    public uint TargetId { get; private set; }

    public RequestAttackPacket()
        : base(0x05, 5) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 4)
        {
            return false;
        }

        TargetId = reader.ReadUInt32();

        return reader.Remaining == 0;
    }
}
