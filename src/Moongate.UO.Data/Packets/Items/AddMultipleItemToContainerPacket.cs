using DryIoc.ImTools;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;

namespace Moongate.UO.Data.Packets.Items;

public class AddMultipleItemToContainerPacket : BaseUoPacket
{
    public UOItemEntity Container { get; set; }


    public AddMultipleItemToContainerPacket() : base(0x3C)
    {
    }

    public AddMultipleItemToContainerPacket(UOItemEntity container) : this()
    {
        Container = container;
    }


    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        var itemQuantity = new Dictionary<UOItemEntity, int>();

        foreach (var (point, itemRef) in Container.ContainedItems)
        {
            var item = itemRef.ToEntity();
            if (!itemQuantity.TryAdd(item, 1))
            {
                itemQuantity[item]++;
            }
        }

        var totalItems = itemQuantity.Sum(kvp => kvp.Value);

        var length = 5 + (totalItems * 20);

        writer.Write(OpCode);
        writer.Write((short)length);
        writer.Write((short)totalItems);

        var gridIndex = 0;

        foreach (var item in itemQuantity)
        {
            writer.Write(item.Key.Id.Value);
            writer.Write((short)item.Key.ItemId);
            writer.Write((byte)0x00);                  // Unknown byte
            writer.Write((short)item.Value);           // Amount (stack)
            writer.Write((short)item.Key.Location.X);
            writer.Write((short)item.Key.Location.Y);
           // writer.Write((short)Container.Location.X); // X Location
          //  writer.Write((short)Container.Location.Y); // Y Location
            writer.Write((byte)gridIndex);             // Backpack grid index (assumed 0 for simplicity)
            writer.Write(Container.Id.Value);          // Container Serial
            writer.Write((short)item.Key.Hue);         // Item Color

            gridIndex++;
        }


        return writer.ToArray();
    }
}
