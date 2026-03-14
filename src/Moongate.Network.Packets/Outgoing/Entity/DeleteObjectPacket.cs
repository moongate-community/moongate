using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x1D, PacketSizing.Fixed, Length = 5, Description = "Delete Object")]
public sealed class DeleteObjectPacket : BaseGameNetworkPacket
{
    public Serial Serial { get; set; } = Serial.Zero;

    public DeleteObjectPacket()
        : base(0x1D, 5) { }

    public DeleteObjectPacket(Serial serial)
        : this()
    {
        Serial = serial;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Serial.Value);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 4)
        {
            return false;
        }

        Serial = (Serial)reader.ReadUInt32();

        return reader.Remaining == 0;
    }
}
