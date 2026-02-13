using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Packets.Characters;

public class SingleClickPacket : BaseUoPacket
{
    public Serial TargetSerial { get; set; }

    public SingleClickPacket() : base(0x05) { }

    protected override bool Read(SpanReader reader)
    {
        TargetSerial = (Serial)reader.ReadUInt32();

        return true;
    }
}
