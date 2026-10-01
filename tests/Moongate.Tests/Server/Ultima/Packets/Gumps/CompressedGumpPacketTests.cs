using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Tests.Server.Ultima.Packets.Gumps;

public sealed class CompressedGumpPacketTests
{
    [Fact]
    public void Encode_CompressesTheLayoutAndTheStrings()
    {
        var built = new GumpLayout().Add(new GumpPage()).Add(new GumpText { Text = "Hi" }).Build();

        var bytes = PacketCodec.Encode(new CompressedGumpPacket(1, 2, 10, 20, built));

        Assert.Equal(0xDD, bytes[0]);
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)));
        Assert.Equal("00000001000000020000000A00000014", Convert.ToHexString(bytes, 3, 16));

        var position = 19;
        var layout = ReadSection(bytes, ref position);
        Assert.Equal("{ page 0 }{ text 0 0 0 0 }\0", Encoding.ASCII.GetString(layout));

        Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position)));
        position += 4;
        var strings = ReadSection(bytes, ref position);
        Assert.Equal("0002" + "00480069", Convert.ToHexString(strings));
        Assert.Equal(bytes.Length, position);
    }

    [Fact]
    public void Encode_WithNoStrings_SendsAZeroLengthTable()
    {
        var built = new GumpLayout().Add(new GumpPage()).Build();

        var bytes = PacketCodec.Encode(new CompressedGumpPacket(1, 2, 0, 0, built));

        var position = 19;
        ReadSection(bytes, ref position);
        Assert.Equal("0000000000000000", Convert.ToHexString(bytes, position, 8));
        Assert.Equal(bytes.Length, position + 8);
    }

    // A compressed section: its compressed length plus 4, its plain length, then the zlib data.
    private static byte[] ReadSection(byte[] bytes, ref int position)
    {
        var compressed = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position)) - 4;
        var plain = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + 4));
        using var zlib = new ZLibStream(new MemoryStream(bytes, position + 8, compressed), CompressionMode.Decompress);
        var output = new byte[plain];
        zlib.ReadExactly(output);
        position += 8 + compressed;

        return output;
    }
}
