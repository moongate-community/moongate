using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Gumps;

namespace Moongate.Server.Ultima.Packets.Gumps;

/// <summary>
///     Opens a gump on clients before 5.0.0a (0xB0): the layout as text and the string table in UTF-16, uncompressed.
/// </summary>
[PacketHandler(0xB0, PacketSizing.Variable, MinimumLength = 23)]
public sealed class GumpPacket : BasePacket<GumpPacket>, IOutgoingPacket
{
    private readonly byte[] _layout;
    private readonly byte[][] _strings;

    public override int Length { get; }

    public uint Serial { get; }

    public uint TypeId { get; }

    public int X { get; }

    public int Y { get; }

    public GumpPacket(uint serial, uint typeId, int x, int y, GumpBuildResult gump)
    {
        Serial = serial;
        TypeId = typeId;
        X = x;
        Y = y;
        _layout = Encoding.UTF8.GetBytes(gump.Layout + "\0");
        _strings = gump.Strings.Select(Encoding.BigEndianUnicode.GetBytes).ToArray();
        Length = 21 + _layout.Length + 2 + _strings.Sum(text => 2 + text.Length);

        if (Length > ushort.MaxValue)
        {
            throw new ArgumentException("The gump is too large for an uncompressed packet.", nameof(gump));
        }
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt32BigEndian(Serial);
        writer.WriteUInt32BigEndian(TypeId);
        writer.WriteUInt32BigEndian((uint)X);
        writer.WriteUInt32BigEndian((uint)Y);
        writer.WriteUInt16BigEndian((ushort)_layout.Length);
        writer.WriteBytes(_layout);
        writer.WriteUInt16BigEndian((ushort)_strings.Length);

        foreach (var text in _strings)
        {
            writer.WriteUInt16BigEndian((ushort)(text.Length / 2));
            writer.WriteBytes(text);
        }
    }
}
