using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Combat;

/// <summary>
/// Outbound swing notification packet (opcode 0x2F).
/// </summary>
public sealed class FightOccurringPacket : BaseGameNetworkPacket
{
    public Serial AttackerId { get; set; } = Serial.Zero;

    public Serial DefenderId { get; set; } = Serial.Zero;

    public FightOccurringPacket()
        : base(0x2F, 10) { }

    public FightOccurringPacket(Serial attackerId, Serial defenderId)
        : this()
    {
        AttackerId = attackerId;
        DefenderId = defenderId;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)0);
        writer.Write(AttackerId.Value);
        writer.Write(DefenderId.Value);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 9)
        {
            return false;
        }

        _ = reader.ReadByte();
        AttackerId = (Serial)reader.ReadUInt32();
        DefenderId = (Serial)reader.ReadUInt32();

        return reader.Remaining == 0;
    }
}
