using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Items;

public class WornItemsPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public WornItemsPacket() : base(0x2E) { }

    public WornItemsPacket(UOMobileEntity mobile) : this()
        => Mobile = mobile;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Grow(Mobile.Equipment.Count * 15);

        foreach (var item in Mobile.Equipment)
        {
            if (item.Key == ItemLayerType.Backpack || item.Key == ItemLayerType.Bank)
            {
                continue; // Skip backpack and bank box
            }

            var itemSpanWriter = new SpanWriter(1, true);
            writer.Write(new WornItemPacket(Mobile, item.Value, item.Key).Write(itemSpanWriter).Span);
            itemSpanWriter.Dispose();
        }

        return writer.ToArray();
    }
}
