using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class UnicodeSpeechMessagePacketTests
{
    [Fact]
    public void PacketId_Is0xAE()
        => Assert.Equal(0xAE, UnicodeSpeechMessagePacket.PacketId);

    [Fact]
    public void Write_EncodesFieldsInOrder()
    {
        var bytes = Serialize(
            new UnicodeSpeechMessagePacket(new(0x1000), 400, ChatMessageType.Regular, new(0x44), "Hero", "Hi")
        );

        Assert.Equal(0xAE, bytes[0]);
        Assert.Equal(0x1000u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(3)));
        Assert.Equal((ushort)400, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(7)));
        Assert.Equal((byte)ChatMessageType.Regular, bytes[9]);
        Assert.Equal((ushort)0x44, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10)));
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(12))); // font, constant

        var language = System.Text.Encoding.ASCII.GetString(bytes.AsSpan(14, 4)).TrimEnd('\0');
        Assert.Equal("ENU", language);

        var name = System.Text.Encoding.ASCII.GetString(bytes.AsSpan(18, 30)).TrimEnd('\0');
        Assert.Equal("Hero", name);

        // "Hi" (4 bytes) + a 2-byte null terminator = 6 bytes of big-endian unicode.
        var text = System.Text.Encoding.BigEndianUnicode.GetString(bytes.AsSpan(48, 6));
        Assert.Equal("Hi\0", text);
    }

    [Fact]
    public void Write_LengthCoversTheWholePacketIncludingTheTerminatedText()
    {
        var bytes = Serialize(
            new UnicodeSpeechMessagePacket(new(0x1000), 400, ChatMessageType.Regular, Hue.Default, "Hero", "Hi")
        );

        // Header (48 bytes) + "Hi" (2 chars) + null terminator, as big-endian unicode = 6 bytes.
        Assert.Equal((ushort)54, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)));
        Assert.Equal(54, bytes.Length);
    }

    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }
}
