using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Packets.Items;

public class PickUpItemPacket : BaseUoPacket
{
    public Serial ItemSerial { get; set; }

    public int StackAmount { get; set; }


    public PickUpItemPacket() : base(0x07)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        /*
         * BYTE[1] 0x07
         * BYTE[4] Item Serial
         * BYTE[4] Stack Amount
         */

        ItemSerial = (Serial)reader.ReadUInt32();
        StackAmount = reader.ReadInt16();

        return true;
    }
}
