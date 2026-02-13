using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Packets.Characters;

public class DoubleClickPacket : BaseUoPacket
{
    public Serial TargetSerial { get; set; }

    public bool IsPaperdoll { get; set; }

    public DoubleClickPacket() : base(0x06) { }

    protected override bool Read(SpanReader reader)
    {
        TargetSerial = (Serial)reader.ReadUInt32();
        IsPaperdoll = (TargetSerial.Value & 0x80000000) != 0;

        return true;
    }
}
