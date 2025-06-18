using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets;

public class SelectServerPacket : BaseUoPacket
{
    public int SelectedServerIndex { get; set; }

    public SelectServerPacket() : base(0xA0)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        SelectedServerIndex = reader.ReadInt16LE();

        return true;
    }
}
