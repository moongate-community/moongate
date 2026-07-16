using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Map patches (0xBF sub-command 0x18): declares the static/land map-diff block counts for the four
/// classic facets. Moongate ships no diff files, so every count is zero and the client uses the base
/// maps. 41 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.WorldState)]
public readonly record struct MapPatchesPacket : IOutgoingPacket
{
    public const byte PacketId = 0xBF;

    private const ushort SubCommand = 0x18;
    private const ushort Length = 41;
    private const int MapCount = 4;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Length);
        writer.Write(SubCommand);
        writer.Write(MapCount);

        for (var i = 0; i < MapCount; i++)
        {
            writer.Write(0); // static patch blocks
            writer.Write(0); // land patch blocks
        }
    }
}
