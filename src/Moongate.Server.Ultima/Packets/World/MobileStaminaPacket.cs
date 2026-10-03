using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The stamina of a mobile (0xA3, 9 bytes): the maximum, then the current value. Sent to the mobile's own player.
/// </summary>
[PacketHandler(0xA3, PacketSizing.Fixed, Length = 9)]
public sealed class MobileStaminaPacket : BaseFixedPacket<MobileStaminaPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public int Stamina { get; }

    public int StaminaMax { get; }

    public MobileStaminaPacket(Serial serial, int stamina, int staminaMax)
    {
        Serial = serial;
        Stamina = stamina;
        StaminaMax = staminaMax;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)StaminaMax);
        writer.WriteUInt16BigEndian((ushort)Stamina);
    }
}
