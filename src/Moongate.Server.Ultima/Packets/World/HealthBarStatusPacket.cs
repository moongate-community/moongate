using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Turns a colour of a mobile's health bar on or off (0x17): green with the poison's level plus one, 0 to take it
///     away.
/// </summary>
[PacketHandler(0x17, PacketSizing.Fixed, Length = 12)]
public sealed class HealthBarStatusPacket : BaseFixedPacket<HealthBarStatusPacket>, IOutgoingPacket
{
    public uint Serial { get; }

    public HealthBarType Kind { get; }

    public int Level { get; }

    public HealthBarStatusPacket(uint serial, HealthBarType kind, int level)
    {
        Serial = serial;
        Kind = kind;
        Level = level;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt32BigEndian(Serial);
        writer.WriteUInt16BigEndian(1);
        writer.WriteUInt16BigEndian((ushort)Kind);
        writer.WriteByte((byte)Level);
    }
}
