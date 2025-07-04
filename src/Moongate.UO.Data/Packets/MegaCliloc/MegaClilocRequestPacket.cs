using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Packets.MegaCliloc;

public class MegaClilocRequestPacket : BaseUoPacket
{

    public List<Serial> Query { get; set; } = new();

    public MegaClilocRequestPacket() : base(0xD6)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        var length = reader.ReadInt16();

        var serialToRead = length / 4;

        foreach (var _ in Enumerable.Range(0, serialToRead))
        {
            Query.Add((Serial)reader.ReadUInt32());
        }

        return true;
    }
}
