using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Spans;

public class PacketReaderTests
{
    [Fact]
    public void AsciiReads_EmptyAndFinalTerminatedText_AreValidWireValues()
    {
        var exact = new PacketReader([]);
        var terminated = new PacketReader([0x41, 0x42, 0x00]);

        Assert.True(exact.TryReadAscii(0, out var empty));
        Assert.Equal("", empty);
        Assert.True(terminated.TryReadNullTerminatedAscii(3, out var text));
        Assert.Equal("AB", text);
    }

    [Fact]
    public void AsciiReads_InvalidText_DoNotAdvance()
    {
        var exactWithNull = new PacketReader([0x41, 0x00]);
        var exactNonAscii = new PacketReader([0x41, 0x80]);
        var fixedNonAscii = new PacketReader([0x41, 0x80]);
        var unterminated = new PacketReader([0x41, 0x42]);
        var embeddedTerminator = new PacketReader([0x41, 0x00, 0x00]);

        Assert.False(exactWithNull.TryReadAscii(2, out _));
        Assert.False(exactNonAscii.TryReadAscii(2, out _));
        Assert.False(fixedNonAscii.TryReadFixedAscii(2, out _));
        Assert.False(unterminated.TryReadNullTerminatedAscii(2, out _));
        Assert.False(embeddedTerminator.TryReadNullTerminatedAscii(3, out _));
        Assert.Equal(0, exactWithNull.Position);
        Assert.Equal(0, exactNonAscii.Position);
        Assert.Equal(0, fixedNonAscii.Position);
        Assert.Equal(0, unterminated.Position);
        Assert.Equal(0, embeddedTerminator.Position);
    }

    [Fact]
    public void AsciiReads_NegativeOrShortField_DoNotAdvance()
    {
        var reader = new PacketReader([0x41]);

        Assert.False(reader.TryReadAscii(-1, out _));
        Assert.False(reader.TryReadFixedAscii(2, out _));
        Assert.False(reader.TryReadNullTerminatedAscii(2, out _));
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void FailedReads_ShortOrNegativeInput_DoNotAdvance()
    {
        var reader = new PacketReader([0x01]);

        Assert.False(reader.TryReadUInt16BigEndian(out _));
        Assert.False(reader.TryReadBytes(-1, out _));
        Assert.False(reader.TryReadBytes(2, out _));
        Assert.Equal(0, reader.Position);
        Assert.Equal(1, reader.Remaining);
    }

    [Fact]
    public void NumericReads_KnownBytes_UseExplicitEndianOrder()
    {
        var reader = new PacketReader([0x12, 0x34, 0x12, 0x34, 0x56, 0x78, 0x78, 0x56, 0x34, 0x12]);

        Assert.True(reader.TryReadUInt16BigEndian(out var value16));
        Assert.True(reader.TryReadUInt32BigEndian(out var value32Big));
        Assert.True(reader.TryReadUInt32LittleEndian(out var value32Little));
        Assert.Equal((ushort)0x1234, value16);
        Assert.Equal(0x12345678u, value32Big);
        Assert.Equal(0x12345678u, value32Little);
        Assert.Equal(10, reader.Position);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void TryReadByte_EmptyInput_ReturnsFalseWithoutAdvancing()
    {
        var reader = new PacketReader([]);

        Assert.False(reader.TryReadByte(out _));
        Assert.False(reader.TryReadUInt32BigEndian(out _));
        Assert.False(reader.TryReadUInt32LittleEndian(out _));
        Assert.False(reader.TryReadSerial(out _));
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void TryReadBytes_ExactRemainingLength_ReturnsSpanAndAdvancesToEnd()
    {
        var reader = new PacketReader([0x12, 0x34, 0x56]);

        Assert.True(reader.TryReadBytes(3, out var bytes));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56 }, bytes.ToArray());
        Assert.Equal(3, reader.Position);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void TryReadFixedAscii_PaddedAndFullWidthFields_ReadsTextAndConsumesField()
    {
        var padded = new PacketReader([0x41, 0x42, 0x00, 0xFF, 0x00]);
        var full = new PacketReader([0x41, 0x42, 0x43]);

        Assert.True(padded.TryReadFixedAscii(5, out var paddedText));
        Assert.Equal("AB", paddedText);
        Assert.Equal(5, padded.Position);
        Assert.True(full.TryReadFixedAscii(3, out var fullText));
        Assert.Equal("ABC", fullText);
    }

    [Fact]
    public void TryReadSerial_KnownBytes_ReadsFourByteBigEndianValue()
    {
        var reader = new PacketReader([0x40, 0x00, 0x00, 0x01]);

        Assert.True(reader.TryReadSerial(out var serial));
        Assert.Equal(new(0x40000001), serial);
    }
}
