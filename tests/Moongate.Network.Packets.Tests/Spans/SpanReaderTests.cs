using System.Buffers.Binary;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Core.Text;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Spans;

public class SpanReaderTests
{
    [Fact]
    public void ReadString_DecoderFailure_DoesNotAdvanceCursor()
    {
        var reader = new SpanReader([0x42, 0xFF, 0x00, 0x43]);
        reader.ReadByte();
        var threw = false;

        try
        {
            reader.ReadString(new UTF8Encoding(false, true));
        }
        catch (DecoderFallbackException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, reader.Position);
        Assert.Equal(0xFF, reader.ReadByte());
    }

    [Theory, InlineData(-1), InlineData(0), InlineData(1)]
    public void ReadString_NullEncoding_ThrowsWithoutAdvancing(int fixedLength)
    {
        var reader = new SpanReader([0x42, 0x41, 0x00]);
        reader.ReadByte();
        Exception? failure = null;

        try
        {
            reader.ReadString(null!, fixedLength: fixedLength);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        Assert.IsType<ArgumentNullException>(failure);
        Assert.Equal(1, reader.Position);
        Assert.Equal(0x41, reader.ReadByte());
    }

    [Fact]
    public void Read_CopiesIntoDestination_AdvancesByCount()
    {
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];
        var reader = new SpanReader(data);
        Span<byte> dest = stackalloc byte[3];

        var written = reader.Read(dest);

        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 1, 2, 3 }, dest.ToArray());
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void Read_DestinationLargerThanRemaining_DoesShortRead()
    {
        ReadOnlySpan<byte> data = [1, 2];
        var reader = new SpanReader(data);
        Span<byte> dest = stackalloc byte[8];

        var written = reader.Read(dest);

        Assert.Equal(2, written);
        Assert.Equal(2, reader.Position);
    }

    [Fact]
    public void ReadBoolean_NonZeroByte_ReturnsTrue()
    {
        ReadOnlySpan<byte> data = [0x01, 0x00, 0x7F];
        var reader = new SpanReader(data);

        Assert.True(reader.ReadBoolean());
        Assert.False(reader.ReadBoolean());
        Assert.True(reader.ReadBoolean());
    }

    [Fact]
    public void ReadByte_AdvancesPosition()
    {
        ReadOnlySpan<byte> data = [0x01, 0x02, 0x03];
        var reader = new SpanReader(data);

        Assert.Equal(0x01, reader.ReadByte());
        Assert.Equal(0x02, reader.ReadByte());
        Assert.Equal(2, reader.Position);
        Assert.Equal(1, reader.Remaining);
    }

    [Fact]
    public void ReadByte_PastEnd_Throws()
    {
        ReadOnlySpan<byte> data = [];
        var reader = new SpanReader(data);

        var threw = false;

        try
        {
            reader.ReadByte();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void ReadBytes_NotEnoughData_Throws()
    {
        ReadOnlySpan<byte> data = [1, 2];
        var reader = new SpanReader(data);

        var threw = false;

        try
        {
            reader.ReadBytes(5);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void ReadBytes_ReturnsFreshAllocation()
    {
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];
        var reader = new SpanReader(data);

        var bytes = reader.ReadBytes(3);

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void ReadInt16_BigEndian()
    {
        ReadOnlySpan<byte> data = [0x12, 0x34];
        var reader = new SpanReader(data);

        Assert.Equal((short)0x1234, reader.ReadInt16());
    }

    [Fact]
    public void ReadInt16LE_LittleEndian()
    {
        ReadOnlySpan<byte> data = [0x34, 0x12];
        var reader = new SpanReader(data);

        Assert.Equal((short)0x1234, reader.ReadInt16LE());
    }

    [Fact]
    public void ReadInt32_BigEndian()
    {
        ReadOnlySpan<byte> data = [0x12, 0x34, 0x56, 0x78];
        var reader = new SpanReader(data);

        Assert.Equal(0x12345678, reader.ReadInt32());
    }

    [Fact]
    public void ReadInt32_PastEnd_Throws()
    {
        ReadOnlySpan<byte> data = [0x01, 0x02];
        var reader = new SpanReader(data);

        var threw = false;

        try
        {
            reader.ReadInt32();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void ReadInt32LE_LittleEndian()
    {
        ReadOnlySpan<byte> data = [0x78, 0x56, 0x34, 0x12];
        var reader = new SpanReader(data);

        Assert.Equal(0x12345678, reader.ReadInt32LE());
    }

    [Fact]
    public void ReadInt64_BigEndian()
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, 0x1122334455667788L);

        var reader = new SpanReader(buf);

        Assert.Equal(0x1122334455667788L, reader.ReadInt64());
    }

    [Fact]
    public void ReadInt64LE_RoundTripsThroughLittleEndian()
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, 0x1122334455667788L);

        var reader = new SpanReader(buffer);

        Assert.Equal(0x1122334455667788L, reader.ReadInt64LE());
    }

    [Fact]
    public void ReadSByte_RoundTripsSignedValue()
    {
        ReadOnlySpan<byte> data = [0xFF];
        var reader = new SpanReader(data);

        Assert.Equal((sbyte)-1, reader.ReadSByte());
    }

    [Fact]
    public void ReadString_Ascii_NullTerminated()
    {
        ReadOnlySpan<byte> data = [(byte)'h', (byte)'i', 0x00, (byte)'x', (byte)'y'];
        var reader = new SpanReader(data);

        var read = reader.ReadString(Encoding.ASCII);

        Assert.Equal("hi", read);
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void ReadString_AsciiFixedLength_StopsAtFixedSize()
    {
        ReadOnlySpan<byte> data = [(byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e'];
        var reader = new SpanReader(data);

        var read = reader.ReadString(Encoding.ASCII, fixedLength: 3);

        Assert.Equal("abc", read);
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void ReadString_FixedLengthWithEarlyTerminator_AdvancesFullWindow()
    {
        // Fixed length 5 with terminator at byte 2: returned string truncates at the
        // terminator but the cursor still advances past the entire window.
        ReadOnlySpan<byte> data = [(byte)'a', (byte)'b', 0x00, (byte)'d', (byte)'e'];
        var reader = new SpanReader(data);

        var read = reader.ReadString(Encoding.ASCII, fixedLength: 5);

        Assert.Equal("ab", read);
        Assert.Equal(5, reader.Position);
    }

    [Fact]
    public void ReadString_FixedLengthZero_ReturnsEmptyAndDoesNotAdvance()
    {
        ReadOnlySpan<byte> data = [1, 2, 3];
        var reader = new SpanReader(data);

        var read = reader.ReadString(Encoding.ASCII, fixedLength: 0);

        Assert.Equal("", read);
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void ReadString_UTF8_NullTerminated()
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.UTF8.GetBytes("hello"));
        bytes.Add(0x00);
        bytes.AddRange(Encoding.UTF8.GetBytes("after"));

        var reader = new SpanReader(bytes.ToArray());
        var read = reader.ReadString(Encoding.UTF8);

        Assert.Equal("hello", read);
    }

    [Fact]
    public void ReadString_WithBclUnicodeEncoding_UsesTwoByteTerminator()
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.Unicode.GetBytes("ab"));
        bytes.Add(0x00);
        bytes.Add(0x00);
        bytes.AddRange(Encoding.Unicode.GetBytes("xy"));

        var reader = new SpanReader(bytes.ToArray());
        var read = reader.ReadString(Encoding.Unicode);

        Assert.Equal("ab", read);
    }

    [Fact]
    public void ReadString_WithCustomMoongateUnicodeEncoding_StillUsesTwoByteTerminator()
    {
        // Regression: GetTerminatorWidth used ReferenceEquals against Encoding.Unicode,
        // so Moongate's TextEncoding.Unicode (a different UnicodeEncoding instance)
        // was treated as 1-byte terminator and corrupted reads.
        var bytes = new List<byte>();
        bytes.AddRange(TextEncoding.Unicode.GetBytes("ab"));
        bytes.Add(0x00);
        bytes.Add(0x00);
        bytes.AddRange(TextEncoding.Unicode.GetBytes("xy"));

        var reader = new SpanReader(bytes.ToArray());
        var read = reader.ReadString(TextEncoding.Unicode);

        Assert.Equal("ab", read);
    }

    [Fact]
    public void ReadUInt16_BigEndian()
    {
        ReadOnlySpan<byte> data = [0xFF, 0xFE];
        var reader = new SpanReader(data);

        Assert.Equal((ushort)0xFFFE, reader.ReadUInt16());
    }

    [Fact]
    public void ReadUInt32_BigEndian()
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, 0xDEADBEEF);

        var reader = new SpanReader(buf);

        Assert.Equal(0xDEADBEEF, reader.ReadUInt32());
    }

    [Fact]
    public void ReadUInt64LE_RoundTrips()
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, 0xCAFEBABEDEADBEEFUL);

        var reader = new SpanReader(buf);

        Assert.Equal(0xCAFEBABEDEADBEEFUL, reader.ReadUInt64LE());
    }

    [Fact]
    public void Seek_AbsoluteAndCurrent()
    {
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];
        var reader = new SpanReader(data);

        reader.Seek(2, SeekOrigin.Begin);
        Assert.Equal(3, reader.ReadByte());
        reader.Seek(-1, SeekOrigin.Current);
        Assert.Equal(3, reader.ReadByte());
    }

    [Fact]
    public void Seek_BeyondEnd_Throws()
    {
        ReadOnlySpan<byte> data = [1, 2, 3];
        var reader = new SpanReader(data);

        var threw = false;

        try
        {
            reader.Seek(10, SeekOrigin.Begin);
        }
        catch (IOException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void Seek_End_PositionsRelativeToBufferEnd()
    {
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];
        var reader = new SpanReader(data);

        reader.Seek(-2, SeekOrigin.End);

        Assert.Equal(3, reader.Position);
        Assert.Equal(4, reader.ReadByte());
    }

    [Fact]
    public void Seek_NegativeAbsolute_ClampsToZero()
    {
        ReadOnlySpan<byte> data = [1, 2, 3];
        var reader = new SpanReader(data);
        reader.Seek(2, SeekOrigin.Begin);

        reader.Seek(-10, SeekOrigin.Begin);

        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void NumericReads_KnownBytes_UseExplicitEndianOrder()
    {
        var reader = new SpanReader([0x12, 0x34, 0x12, 0x34, 0x56, 0x78, 0x78, 0x56, 0x34, 0x12]);

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
    public void TryReadSerial_KnownBytes_ReadsFourByteBigEndianValue()
    {
        var reader = new SpanReader([0x40, 0x00, 0x00, 0x01]);

        Assert.True(reader.TryReadSerial(out Serial serial));
        Assert.Equal(new Serial(0x40000001), serial);
    }

    [Fact]
    public void FailedReads_ShortOrNegativeInput_DoNotAdvance()
    {
        var reader = new SpanReader([0x01]);

        Assert.False(reader.TryReadUInt16BigEndian(out _));
        Assert.False(reader.TryReadBytes(-1, out _));
        Assert.False(reader.TryReadBytes(2, out _));
        Assert.Equal(0, reader.Position);
        Assert.Equal(1, reader.Remaining);
    }

    [Fact]
    public void TryReadBytes_ExactRemainingLength_ReturnsSpanAndAdvancesToEnd()
    {
        var reader = new SpanReader([0x12, 0x34, 0x56]);

        Assert.True(reader.TryReadBytes(3, out var bytes));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56 }, bytes.ToArray());
        Assert.Equal(3, reader.Position);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void TryReadByte_EmptyInput_ReturnsFalseWithoutAdvancing()
    {
        var reader = new SpanReader([]);

        Assert.False(reader.TryReadByte(out _));
        Assert.False(reader.TryReadUInt32BigEndian(out _));
        Assert.False(reader.TryReadUInt32LittleEndian(out _));
        Assert.False(reader.TryReadSerial(out _));
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void TryReadFixedAscii_PaddedAndFullWidthFields_ReadsTextAndConsumesField()
    {
        var padded = new SpanReader([0x41, 0x42, 0x00, 0xFF, 0x00]);
        var full = new SpanReader([0x41, 0x42, 0x43]);

        Assert.True(padded.TryReadFixedAscii(5, out var paddedText));
        Assert.Equal("AB", paddedText);
        Assert.Equal(5, padded.Position);
        Assert.True(full.TryReadFixedAscii(3, out var fullText));
        Assert.Equal("ABC", fullText);
    }

    [Fact]
    public void AsciiReads_InvalidText_DoNotAdvance()
    {
        var exactWithNull = new SpanReader([0x41, 0x00]);
        var exactNonAscii = new SpanReader([0x41, 0x80]);
        var fixedNonAscii = new SpanReader([0x41, 0x80]);
        var unterminated = new SpanReader([0x41, 0x42]);
        var embeddedTerminator = new SpanReader([0x41, 0x00, 0x00]);

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
        var reader = new SpanReader([0x41]);

        Assert.False(reader.TryReadAscii(-1, out _));
        Assert.False(reader.TryReadFixedAscii(2, out _));
        Assert.False(reader.TryReadNullTerminatedAscii(2, out _));
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void AsciiReads_EmptyAndFinalTerminatedText_AreValidWireValues()
    {
        var exact = new SpanReader([]);
        var terminated = new SpanReader([0x41, 0x42, 0x00]);

        Assert.True(exact.TryReadAscii(0, out var empty));
        Assert.Equal("", empty);
        Assert.True(terminated.TryReadNullTerminatedAscii(3, out var text));
        Assert.Equal("AB", text);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void ReadString_OverflowingFixedLength_ThrowsWithoutAdvancing(bool utf32)
    {
        var reader = new SpanReader([0x42, 0, 0, 0]);
        reader.ReadByte();
        var threw = false;

        try
        {
            reader.ReadString(utf32 ? Encoding.UTF32 : TextEncoding.Unicode, fixedLength: 1 << 30);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, reader.Position);
        Assert.Equal(3, reader.Remaining);
    }

    [Theory, InlineData(-2), InlineData(int.MinValue)]
    public void ReadString_InvalidNegativeLength_ThrowsWithoutAdvancing(int fixedLength)
    {
        var reader = new SpanReader([0x42, 0x41, 0]);
        reader.ReadByte();
        var threw = false;

        try
        {
            reader.ReadString(Encoding.ASCII, fixedLength: fixedLength);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, reader.Position);
    }

    [Theory, InlineData(SeekOrigin.Current), InlineData(SeekOrigin.End)]
    public void Seek_OverflowingOffset_ThrowsWithoutAdvancing(SeekOrigin origin)
    {
        var reader = new SpanReader([0x42, 0x41, 0]);
        reader.ReadByte();
        var threw = false;

        try
        {
            reader.Seek(int.MaxValue, origin);
        }
        catch (IOException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, reader.Position);
        Assert.Equal(0x41, reader.ReadByte());
    }

    [Fact]
    public void Seek_InvalidOrigin_ThrowsWithoutAdvancing()
    {
        var reader = new SpanReader([0x42, 0x41, 0]);
        reader.ReadByte();
        var threw = false;

        try
        {
            reader.Seek(0, (SeekOrigin)int.MaxValue);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, reader.Position);
    }

    [Theory, InlineData(-1), InlineData(int.MinValue), InlineData(int.MaxValue)]
    public void TryReads_InvalidLengthsAtNonzeroPosition_ReturnFalseWithoutAdvancing(int length)
    {
        var reader = new SpanReader([0x42, 0x41, 0]);
        reader.ReadByte();

        Assert.False(reader.TryReadBytes(length, out var bytes));
        Assert.False(reader.TryReadAscii(length, out var exact));
        Assert.False(reader.TryReadFixedAscii(length, out var fixedText));
        Assert.False(reader.TryReadNullTerminatedAscii(length, out var terminated));
        Assert.True(bytes.IsEmpty);
        Assert.Null(exact);
        Assert.Null(fixedText);
        Assert.Null(terminated);
        Assert.Equal(1, reader.Position);
        Assert.Equal(2, reader.Remaining);
    }

    [Fact]
    public void Dispose_RepeatedCalls_ClearBufferAndAllowSafeFailedReads()
    {
        var reader = new SpanReader([0x42, 0x41, 0]);
        reader.ReadByte();

        reader.Dispose();
        reader.Dispose();

        Assert.Equal(0, reader.Length);
        Assert.Equal(0, reader.Position);
        Assert.Equal(0, reader.Remaining);
        Assert.True(reader.Buffer.IsEmpty);
        Assert.False(reader.TryReadByte(out _));
        Assert.False(reader.TryReadUInt16BigEndian(out _));
        Assert.False(reader.TryReadUInt32BigEndian(out _));
        Assert.False(reader.TryReadUInt32LittleEndian(out _));
        Assert.False(reader.TryReadSerial(out _));
        Assert.False(reader.TryReadNullTerminatedAscii(1, out _));
    }
}
