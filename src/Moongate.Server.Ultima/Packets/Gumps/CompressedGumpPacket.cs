using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Gumps;

namespace Moongate.Server.Ultima.Packets.Gumps;

/// <summary>
///     Opens a gump on clients from 5.0.0a (0xDD): the layout and the string table compressed with zlib, each after its
///     compressed length plus 4 and its plain length; an empty string table is a zero length alone.
/// </summary>
[PacketHandler(0xDD, PacketSizing.Variable, MinimumLength = 35)]
public sealed class CompressedGumpPacket : BasePacket<CompressedGumpPacket>, IOutgoingPacket
{
    private readonly byte[] _layout;
    private readonly int _layoutLength;
    private readonly byte[] _strings;
    private readonly int _stringsLength;
    private readonly int _stringCount;

    public override int Length { get; }

    public uint Serial { get; }

    public uint TypeId { get; }

    public int X { get; }

    public int Y { get; }

    public CompressedGumpPacket(uint serial, uint typeId, int x, int y, GumpBuildResult gump)
    {
        Serial = serial;
        TypeId = typeId;
        X = x;
        Y = y;

        var layout = Encoding.UTF8.GetBytes(gump.Layout + "\0");
        _layoutLength = layout.Length;
        _layout = Compress(layout);
        _stringCount = gump.Strings.Count;

        var strings = new MemoryStream();

        foreach (var text in gump.Strings)
        {
            var bytes = Encoding.BigEndianUnicode.GetBytes(text);
            Span<byte> length = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(bytes.Length / 2));
            strings.Write(length);
            strings.Write(bytes);
        }

        _stringsLength = (int)strings.Length;
        _strings = _stringCount == 0 ? [] : Compress(strings.ToArray());
        Length = 19 + 8 + _layout.Length + 4 + 4 + (_stringCount == 0 ? 0 : 4 + _strings.Length);

        if (Length > ushort.MaxValue)
        {
            throw new ArgumentException("The gump is too large for a packet.", nameof(gump));
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
        writer.WriteUInt32BigEndian((uint)(_layout.Length + 4));
        writer.WriteUInt32BigEndian((uint)_layoutLength);
        writer.WriteBytes(_layout);
        writer.WriteUInt32BigEndian((uint)_stringCount);

        if (_stringCount == 0)
        {
            writer.WriteUInt32BigEndian(0);

            return;
        }

        writer.WriteUInt32BigEndian((uint)(_strings.Length + 4));
        writer.WriteUInt32BigEndian((uint)_stringsLength);
        writer.WriteBytes(_strings);
    }

    private static byte[] Compress(byte[] data)
    {
        var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
