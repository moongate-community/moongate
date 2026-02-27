using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming.Interaction;

[PacketHandler(0x09, PacketSizing.Fixed, Length = 5, Description = "Single Click")]

/// <summary>
/// Represents SingleClickPacket.
/// </summary>
public class SingleClickPacket : BaseGameNetworkPacket
{
    public Serial TargetSerial { get; set; }

    public SingleClickPacket()
        : base(0x09, 5) { }

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
