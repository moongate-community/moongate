using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Combat;

/// <summary>
///     Tells the player who swings that it swings (0x2F, 10 bytes): a flag of zero, the attacker and the defender.
/// </summary>
[PacketHandler(0x2F, PacketSizing.Fixed, Length = 10, Description = "Swing")]
public sealed class SwingPacket : BaseFixedPacket<SwingPacket>, IOutgoingPacket
{
    public Serial Attacker { get; }

    public Serial Defender { get; }

    public SwingPacket(Serial attacker, Serial defender)
    {
        Attacker = attacker;
        Defender = defender;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(0);
        writer.WriteSerial(Attacker);
        writer.WriteSerial(Defender);
    }
}
