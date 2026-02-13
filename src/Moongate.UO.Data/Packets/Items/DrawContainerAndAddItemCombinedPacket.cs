using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Items;

public class DrawContainerAndAddItemCombinedPacket : BaseUoPacket
{
    public UOItemEntity Container { get; set; }

    public DrawContainerAndAddItemCombinedPacket(UOItemEntity container) : base(0x01) // Meta packet opcode
        => Container = container;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        using var drawContainerWriter = new SpanWriter(1, true);
        var drawContainerData = new DrawContainer(Container).Write(drawContainerWriter);

        using var addItemsWriter = new SpanWriter(1, true);
        var addItemsData = new AddMultipleItemToContainerPacket(Container).Write(addItemsWriter);

        using var combinedWriter = new SpanWriter(drawContainerData.Length + addItemsData.Length);
        combinedWriter.Write(drawContainerData.Span);
        combinedWriter.Write(addItemsData.Span);

        return combinedWriter.ToArray();
    }
}
