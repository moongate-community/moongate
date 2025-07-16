using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Items;

public class DrawContainerAndAddItemCombinedPacket : BaseUoPacket
{
    public UOItemEntity Container { get; set; }

    public DrawContainerAndAddItemCombinedPacket(UOItemEntity container) : base(0x01) // Meta packet opcode
    {
        Container = container;
    }


    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        using var tmpSpanWriter = new SpanWriter(1, true);

        tmpSpanWriter.Write(new DrawContainer(Container).Write(tmpSpanWriter).ToArray());
        tmpSpanWriter.Write(new AddMultipleItemToContainerPacket(Container).Write(tmpSpanWriter).ToArray());

        return tmpSpanWriter.ToArray();
    }
}
