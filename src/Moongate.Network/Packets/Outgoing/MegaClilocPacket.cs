using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Mega cliloc (0xD6): the property list ("tooltip") of one object — cliloc lines with UTF-16LE
/// arguments, preceded by the content hash the client caches against. Variable length. The hash
/// travels raw here; the 0xDC notification carries the same value flagged with 0x40000000.
/// </summary>
[PacketDocumentation(PacketFamilyType.Tooltips, IsVariableLength = true)]
public readonly record struct MegaClilocPacket(Serial Serial, int Hash, IReadOnlyList<OplEntry> Entries)
    : IOutgoingPacket
{
    public const byte PacketId = 0xD6;

    private const int HeaderLength = 15;    // id + length + 0x0001 + serial + 0x0000 + hash
    private const int EntryOverhead = 6;    // cliloc + byte length
    private const int TerminatorLength = 4;

    public void Write(ref SpanWriter writer)
    {
        var length = HeaderLength + TerminatorLength;

        foreach (var entry in Entries)
        {
            length += EntryOverhead + entry.Arguments.Length * 2;
        }

        writer.Write(PacketId);
        writer.Write((ushort)length);
        writer.Write((ushort)0x0001);
        writer.Write(Serial);
        writer.Write((ushort)0x0000);
        writer.Write(Hash);

        foreach (var entry in Entries)
        {
            writer.Write(entry.Cliloc);
            writer.Write((ushort)(entry.Arguments.Length * 2));
            writer.WriteLittleUni(entry.Arguments);
        }

        writer.Write(0);
    }
}
