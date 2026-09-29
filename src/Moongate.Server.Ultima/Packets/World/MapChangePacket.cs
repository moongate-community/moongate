using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Switches the client to a map: general information packet 0xBF, subcommand 0x08 (6 bytes).
/// </summary>
[PacketHandler(0xBF, PacketSizing.Variable, MinimumLength = 5)]
public sealed class MapChangePacket : BasePacket<MapChangePacket>, IOutgoingPacket
{
    private const ushort Subcommand = 0x08;

    public override int Length => 6;

    public MapType Map { get; }

    public MapChangePacket(MapType map)
    {
        Map = map;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt16BigEndian(Subcommand);
        writer.WriteByte((byte)Map);
    }
}
