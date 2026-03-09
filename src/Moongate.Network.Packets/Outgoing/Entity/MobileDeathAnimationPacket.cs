using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0xAF, PacketSizing.Fixed, Length = 13, Description = "Death Animation")]
public sealed class MobileDeathAnimationPacket : BaseGameNetworkPacket
{
    public Serial KilledMobileId { get; set; } = Serial.Zero;

    public Serial CorpseId { get; set; } = Serial.Zero;

    public MobileDeathAnimationPacket()
        : base(0xAF, 13) { }

    public MobileDeathAnimationPacket(Serial killedMobileId, Serial corpseId)
        : this()
    {
        KilledMobileId = killedMobileId;
        CorpseId = corpseId;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(KilledMobileId.Value);
        writer.Write(CorpseId.Value);
        writer.Write(0); // reserved
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 12)
        {
            return false;
        }

        KilledMobileId = (Serial)reader.ReadUInt32();
        CorpseId = (Serial)reader.ReadUInt32();
        _ = reader.ReadUInt32(); // reserved

        return reader.Remaining == 0;
    }
}
