using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Map (facet) change (0xBF sub-command 0x08): switches the client to the given map. 6 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.WorldState, Length = 6, SubCommand = 0x08)]
public readonly record struct MapChangePacket(MapType Map) : IOutgoingPacket
{
    public const byte PacketId = 0xBF;

    private const ushort SubCommand = 0x08;
    private const ushort Length = 6;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Length);
        writer.Write(SubCommand);
        writer.Write((byte)Map);
    }
}
