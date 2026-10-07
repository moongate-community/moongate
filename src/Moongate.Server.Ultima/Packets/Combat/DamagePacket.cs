using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Combat;

/// <summary>
///     The damage a mobile took, which the client shows over its head (0x0B, 7 bytes): the serial and the damage,
///     kept from 0 to 65535.
/// </summary>
[PacketHandler(0x0B, PacketSizing.Fixed, Length = 7, Description = "Damage")]
public sealed class DamagePacket : BaseFixedPacket<DamagePacket>, IOutgoingPacket
{
    public Serial Target { get; }

    public int Damage { get; }

    public DamagePacket(Serial target, int damage)
    {
        Target = target;
        Damage = damage;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Target);
        writer.WriteUInt16BigEndian((ushort)Math.Clamp(Damage, 0, ushort.MaxValue));
    }
}
