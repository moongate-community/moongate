using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The mana of a mobile (0xA2, 9 bytes): the maximum, then the current value. Sent to the mobile's own player.
/// </summary>
[PacketHandler(0xA2, PacketSizing.Fixed, Length = 9)]
public sealed class MobileManaPacket : BaseFixedPacket<MobileManaPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public int Mana { get; }

    public int ManaMax { get; }

    public MobileManaPacket(Serial serial, int mana, int manaMax)
    {
        Serial = serial;
        Mana = mana;
        ManaMax = manaMax;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)ManaMax);
        writer.WriteUInt16BigEndian((ushort)Mana);
    }
}
