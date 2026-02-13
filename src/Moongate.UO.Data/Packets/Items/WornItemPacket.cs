using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Items;

public class WornItemPacket : BaseUoPacket
{
    public ItemReference Item { get; set; }
    public ItemLayerType Layer { get; set; }
    public UOMobileEntity Mobile { get; set; }

    public WornItemPacket() : base(0x2E) { }

    public WornItemPacket(UOMobileEntity mobile, ItemReference item, ItemLayerType layer) : this()
    {
        Mobile = mobile;
        Item = item;
        Layer = layer;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        // BYTE cmd
        // BYTE[4] itemid (always starts 0x40 in my data)
        // BYTE[2] model (item hex #)
        // BYTE[1] (0x00)
        // BYTE[1] layer
        // BYTE[4] playerID
        // BYTE[2] color/hue

        writer.Write(OpCode);
        writer.Write(Item.Id.Value);
        writer.Write((ushort)Item.ItemId);
        writer.Write((byte)0x00);
        writer.Write((byte)Layer);
        writer.Write(Mobile.Id.Value);
        writer.Write((ushort)Item.Hue);

        return writer.ToArray();
    }
}
