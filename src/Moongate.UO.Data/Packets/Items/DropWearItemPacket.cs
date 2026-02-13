using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Items;

public class DropWearItemPacket : BaseUoPacket
{
    public Serial ItemId { get; set; }
    public ItemLayerType Layer { get; set; }
    public Serial MobileId { get; set; }

    public DropWearItemPacket() : base(0x13) { }

    protected override bool Read(SpanReader reader)
    {
        ItemId = (Serial)reader.ReadUInt32();
        Layer = (ItemLayerType)reader.ReadByte();
        MobileId = (Serial)reader.ReadUInt32();

        return true;
    }
}
