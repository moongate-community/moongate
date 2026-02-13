using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Items;

public class DropItemApprovedPacket : BaseUoPacket
{
    public DropItemApprovedPacket() : base(0x29) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);

        return writer.ToArray();
    }
}
