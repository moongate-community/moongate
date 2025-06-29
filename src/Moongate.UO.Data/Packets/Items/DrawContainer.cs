using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Items;

public class DrawContainer : BaseUoPacket
{
    public UOItemEntity Container { get; set; }

    public DrawContainer() : base(0x24)
    {
    }

    public DrawContainer(UOItemEntity container) : this()
    {
        Container = container;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {

        writer.Write(OpCode);
        writer.Write(Container.Id.Value);
        writer.Write(Container.GumpId.Value);
        return writer.ToArray();
    }
}
