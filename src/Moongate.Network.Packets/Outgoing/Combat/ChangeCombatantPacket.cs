using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Combat;

/// <summary>
/// Outbound change-combatant packet (opcode 0xAA).
/// </summary>
public sealed class ChangeCombatantPacket : BaseGameNetworkPacket
{
    public Serial CombatantId { get; set; } = Serial.Zero;

    public ChangeCombatantPacket()
        : base(0xAA, 5) { }

    public ChangeCombatantPacket(Serial combatantId)
        : this()
    {
        CombatantId = combatantId;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(CombatantId.Value);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 4)
        {
            return false;
        }

        CombatantId = (Serial)reader.ReadUInt32();

        return reader.Remaining == 0;
    }
}
