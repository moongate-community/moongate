using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming.Interaction;

[PacketHandler(0x06, PacketSizing.Fixed, Length = 5, Description = "Double Click")]

/// <summary>
/// Represents DoubleClickPacket.
/// </summary>
public class DoubleClickPacket : BaseGameNetworkPacket
{
    public Serial TargetSerial { get; set; }

    public DoubleClickPacket()
        : base(0x06, 5) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 4)
        {
            return false;
        }

        TargetSerial = (Serial)reader.ReadUInt32();

        return reader.Remaining == 0;
    }
}
