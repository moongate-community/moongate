using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The locks of the three stats of the player's own character, as the status window shows them as arrows:
///     general information packet 0xBF, subcommand 0x19 (12 bytes).
/// </summary>
[PacketHandler(0xBF, PacketSizing.Variable, MinimumLength = 5)]
public sealed class StatLockInfoPacket : BasePacket<StatLockInfoPacket>, IOutgoingPacket
{
    private const ushort Subcommand = 0x19;
    private const byte StatLocksType = 2;

    public override int Length => 12;

    public Serial Serial { get; }

    public StatLockType Strength { get; }

    public StatLockType Dexterity { get; }

    public StatLockType Intelligence { get; }

    public StatLockInfoPacket(Serial serial, StatLockType strength, StatLockType dexterity, StatLockType intelligence)
    {
        Serial = serial;
        Strength = strength;
        Dexterity = dexterity;
        Intelligence = intelligence;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt16BigEndian(Subcommand);
        writer.WriteByte(StatLocksType);
        writer.WriteUInt32BigEndian(Serial.Value);
        writer.WriteByte(0);
        // Two bits each: strength, dexterity, intelligence.
        writer.WriteByte((byte)(((int)Strength << 4) | ((int)Dexterity << 2) | (int)Intelligence));
    }
}
