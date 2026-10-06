using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Combat;

/// <summary>
///     Tells a player whom its character is fighting (0xAA, 5 bytes): the serial of the target, or zero when it fights
///     no one. The client highlights the target.
/// </summary>
[PacketHandler(0xAA, PacketSizing.Fixed, Length = 5, Description = "Combatant")]
public sealed class CombatantPacket : BaseFixedPacket<CombatantPacket>, IOutgoingPacket
{
    public Serial Target { get; }

    public CombatantPacket(Serial target)
    {
        Target = target;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Target);
    }
}
