using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// OPL info (0xDC): tells the client the current property-list revision of an object. 9 bytes fixed.
/// The client compares the hash with its cache and requests the full list (0xD6) when it differs.
/// The wire value is the raw content hash flagged with 0x40000000.
/// </summary>
[PacketDocumentation(PacketFamilyType.Tooltips, Length = 9)]
public readonly record struct OplInfoPacket(Serial Serial, int Hash) : IOutgoingPacket
{
    public const byte PacketId = 0xDC;

    private const int RevisionFlag = 0x40000000;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(RevisionFlag | Hash);
    }
}
