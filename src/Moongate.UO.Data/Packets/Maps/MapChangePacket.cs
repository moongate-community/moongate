using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Maps;

namespace Moongate.UO.Data.Packets.Maps;

public class MapChangePacket : BaseUoPacket
{
    public Map Map { get; set; }

    public MapChangePacket(Map map) : base(0xBF)
    {
        Map = map;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)0);
        writer.Write((byte)0x06);
        writer.Write((byte)0);
        writer.Write((byte)0x08);
        writer.Write((byte)Map.MapID);


        return writer.ToArray();
    }
}
