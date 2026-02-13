using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;

namespace Moongate.UO.Data.Packets.Effects;

public class BloodPacket : BaseUoPacket
{
    public Serial Serial { get; set; }

    public BloodPacket(Serial serial) : this()
        => Serial = serial;

    public BloodPacket(ISerialEntity entity) : this()
        => Serial = entity.Id;

    public BloodPacket() : base(0x2A) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Serial.Value);

        return writer.ToArray();
    }
}
