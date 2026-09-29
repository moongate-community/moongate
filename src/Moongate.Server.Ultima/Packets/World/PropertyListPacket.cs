using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Tooltips;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     An AOS tooltip (0xD6): the object, the hash of its lines, then each line as a cliloc number and its arguments
///     in little-endian UTF-16, ended by a zero cliloc; the layout of ModernUO, POL, UOX3 and Source-X.
/// </summary>
[PacketHandler(0xD6, PacketSizing.Variable, MinimumLength = HeaderLength + TerminatorLength)]
public sealed class PropertyListPacket : BasePacket<PropertyListPacket>, IOutgoingPacket
{
    private const int HeaderLength = 15;
    private const int TerminatorLength = 4;

    public override int Length { get; }

    public Serial Serial { get; }

    public int Hash { get; }

    public IReadOnlyList<PropertyEntry> Entries { get; }

    public PropertyListPacket(Serial serial, PropertyList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        Serial = serial;
        Hash = list.Hash;
        Entries = list.Entries.ToArray();
        Length = HeaderLength + Entries.Sum(entry => 6 + entry.Arguments.Length * 2) + TerminatorLength;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt16BigEndian(1);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian(0);
        writer.WriteUInt32BigEndian((uint)Hash);

        foreach (var entry in Entries)
        {
            writer.WriteUInt32BigEndian((uint)entry.Cliloc);
            writer.WriteUInt16BigEndian((ushort)(entry.Arguments.Length * 2));
            writer.WriteBytes(Encoding.Unicode.GetBytes(entry.Arguments));
        }

        writer.WriteUInt32BigEndian(0);
    }
}
