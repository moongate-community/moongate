using Moongate.Network.Packets.Incoming;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class UnicodeSpeechPacketTests
{
    [Fact]
    public void PacketId_Is0xAD()
        => Assert.Equal(0xAD, UnicodeSpeechPacket.PacketId);

    [Fact]
    public void Read_EncodedFlagSet_ReturnsIsEncodedTrueWithoutReadingFurther()
    {
        // type = 0xC0 (Encoded), nothing valid follows — Read must not attempt to parse it.
        var buffer = new byte[] { 0xAD, 0x00, 0x07, 0xC0, 0xFF, 0xFF, 0xFF };

        var reader = new SpanReader(buffer);
        var packet = UnicodeSpeechPacket.Read(ref reader);

        Assert.True(packet.IsEncoded);
        Assert.Equal(string.Empty, packet.Text);
    }

    [Fact]
    public void Read_ParsesTypeHueAndText()
    {
        // 0xAD, length(2)=18 (the whole buffer below), type=0x00 (Regular), hue=0x0044,
        // font=3 (discarded), language "ENU" (4 bytes), text "Hi" as big-endian unicode,
        // null-terminated. Read() never uses the declared length itself (SpanReader is already
        // scoped to the real buffer) but it is kept accurate here for a reader's sanity.
        List<byte> buffer =
        [
            0xAD, 0x00, 0x12,
            0x00,
            0x00, 0x44,
            0x00, 0x03,
            (byte)'E', (byte)'N', (byte)'U', 0x00,
            0x00, (byte)'H', 0x00, (byte)'i', 0x00, 0x00
        ];

        var reader = new SpanReader(buffer.ToArray());
        var packet = UnicodeSpeechPacket.Read(ref reader);

        Assert.False(packet.IsEncoded);
        Assert.Equal(ChatMessageType.Regular, packet.Type);
        Assert.Equal((ushort)0x44, packet.Hue.Value);
        Assert.Equal("Hi", packet.Text);
    }

    [Fact]
    public void Read_TrimsSurroundingWhitespace()
    {
        List<byte> buffer =
        [
            0xAD, 0x00, 0x16,
            0x00,
            0x00, 0x00,
            0x00, 0x03,
            (byte)'E', (byte)'N', (byte)'U', 0x00,
            0x00, (byte)' ', 0x00, (byte)'H', 0x00, (byte)'i', 0x00, (byte)' ', 0x00, 0x00
        ];

        var reader = new SpanReader(buffer.ToArray());
        var packet = UnicodeSpeechPacket.Read(ref reader);

        Assert.Equal("Hi", packet.Text);
    }
}
