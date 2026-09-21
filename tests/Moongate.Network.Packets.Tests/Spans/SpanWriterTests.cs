using System.Runtime.InteropServices;
using System.Text;
using Moongate.Core.Text;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Spans;

public class SpanWriterTests
{
    [Theory, InlineData(0), InlineData(1)]
    public void Capacity_NegativeArgument_DoesNotChangeWrittenBytes(int operation)
    {
        var writer = new SpanWriter(8, true);

        try
        {
            writer.Write((byte)0xAB);
            var capacity = writer.Capacity;
            var threw = false;

            try
            {
                if (operation == 0)
                {
                    writer.EnsureCapacity(-1);
                }
                else
                {
                    writer.Grow(-1);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            Assert.True(threw);
            Assert.Equal(capacity, writer.Capacity);
            Assert.Equal(1, writer.Position);
            Assert.Equal(new byte[] { 0xAB }, writer.Span.ToArray());
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Theory, InlineData(0), InlineData(1)]
    public void Capacity_Overflow_DoesNotChangeWrittenBytes(int operation)
    {
        var writer = new SpanWriter(8, true);

        try
        {
            writer.Write((byte)0xAB);
            var threw = false;

            try
            {
                if (operation == 0)
                {
                    writer.Grow(int.MaxValue);
                }
                else
                {
                    writer.EnsureRemainingCapacity(int.MaxValue);
                }
            }
            catch (OverflowException)
            {
                threw = true;
            }

            Assert.True(threw);
            Assert.Equal(1, writer.Position);
            Assert.Equal(new byte[] { 0xAB }, writer.Span.ToArray());
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void Clear_AdvancesPositionWithZeroes()
    {
        Span<byte> backing = stackalloc byte[8];
        backing.Fill(0xFF);
        var writer = new SpanWriter(backing);

        writer.Clear(3);

        Assert.Equal(3, writer.Position);
        Assert.Equal(0, backing[0]);
        Assert.Equal(0, backing[1]);
        Assert.Equal(0, backing[2]);
        Assert.Equal(0xFF, backing[3]);
    }

    [Fact]
    public void EnsureCapacity_AbsoluteSize_AndEnsureRemainingCapacity_AreDistinct()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);
        writer.Write((byte)1);
        writer.EnsureCapacity(4);
        writer.EnsureRemainingCapacity(3);
        writer.Write(new byte[] { 2, 3, 4 });
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, writer.Span.ToArray());
    }

    [Fact]
    public void EnsureCapacity_ResizeDisabled_TooSmall_Throws()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);

        var threw = false;

        try
        {
            writer.EnsureCapacity(64);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void EnsureRemainingCapacity_NegativeCount_ThrowsWithoutAdvancing()
    {
        Span<byte> destination = stackalloc byte[1];
        var writer = new SpanWriter(destination);

        var exceptionThrown = false;

        try
        {
            writer.EnsureRemainingCapacity(-1);
        }
        catch (ArgumentOutOfRangeException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Exports_AfterHeaderRewrite_PreserveEntireWrittenPayload()
    {
        var writer = new SpanWriter(8, true);
        writer.Write(new byte[] { 0, 2, 3 });
        writer.Seek(0, SeekOrigin.Begin);
        writer.Write((byte)1);
        Assert.Equal(new byte[] { 1, 2, 3 }, writer.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, writer.Span.ToArray());
        using var owner = writer.ToSpan();
        Assert.Equal(new byte[] { 1, 2, 3 }, owner.Span.ToArray());
    }

    [Fact]
    public void FailedPrimitiveWrite_LeavesCountAndDestinationUnchanged()
    {
        var destination = new byte[] { 0xCC, 0xCC, 0xCC };
        var writer = new SpanWriter(destination);

        var exceptionThrown = false;

        try
        {
            writer.WriteUInt32BigEndian(1);
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(new byte[] { 0xCC, 0xCC, 0xCC }, destination);
    }

    [Fact]
    public void Grow_ExplicitGrowthWhenDisabled_ThrowsWithoutChangingBuffer()
    {
        Span<byte> backing = stackalloc byte[2];
        var writer = new SpanWriter(backing);
        writer.Write((byte)0xAB);
        var threw = false;

        try
        {
            writer.Grow(16);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(2, writer.Capacity);
        Assert.Equal(new byte[] { 0xAB }, writer.Span.ToArray());
    }

    [Fact]
    public void Grow_WhenResizeDisabled_Throws()
    {
        Span<byte> backing = stackalloc byte[2];
        var writer = new SpanWriter(backing);

        writer.Write((byte)0xAA);
        writer.Write((byte)0xBB);

        var threw = false;

        try
        {
            writer.Write((byte)0xCC);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void Grow_WhenResizeEnabled_ExpandsBuffer()
    {
        var writer = new SpanWriter(4, true);

        for (var i = 0; i < 20; i++)
        {
            writer.Write((byte)i);
        }

        Assert.Equal(20, writer.BytesWritten);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal((byte)i, writer.Span[i]);
        }

        writer.Dispose();
    }

    [Fact]
    public void NumericWrites_KnownValues_UseExplicitEndianOrder()
    {
        Span<byte> destination = stackalloc byte[14];
        var writer = new SpanWriter(destination);

        writer.WriteUInt16BigEndian(0x1234);
        writer.WriteUInt32BigEndian(0x12345678);
        writer.WriteUInt32LittleEndian(0x12345678);
        writer.WriteSerial(new(0x40000001));

        Assert.Equal(Convert.FromHexString("1234123456787856341240000001"), writer.WrittenSpan.ToArray());
        Assert.Equal(14, writer.WrittenCount);
        Assert.Equal(0, writer.Remaining);
    }

    [Fact]
    public void Position_DoesNotShrinkBytesWritten_AfterRewind()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);
        writer.Write(0x12345678);
        Assert.Equal(4, writer.BytesWritten);

        writer.Seek(2, SeekOrigin.Begin);
        Assert.Equal(4, writer.BytesWritten);

        writer.Write((byte)0xFF);

        // BytesWritten still 4 because we wrote within the already-written region.
        Assert.Equal(4, writer.BytesWritten);
    }

    [Fact]
    public void RoundTrip_AllPrimitives_ThroughReader()
    {
        var writer = new SpanWriter(64, true);

        try
        {
            writer.Write(true);
            writer.Write((byte)0xAB);
            writer.Write((sbyte)-7);
            writer.Write((short)-1234);
            writer.WriteLE((short)-1234);
            writer.Write((ushort)0xBEEF);
            writer.Write(0x11223344);
            writer.WriteLE(0x11223344);
            writer.Write(0xCAFEBABE);
            writer.Write(0x1122334455667788L);
            writer.Write(0xDEADBEEFCAFEBABEUL);

            var written = writer.Span.ToArray();
            var reader = new SpanReader(written);

            Assert.True(reader.ReadBoolean());
            Assert.Equal(0xAB, reader.ReadByte());
            Assert.Equal((sbyte)-7, reader.ReadSByte());
            Assert.Equal((short)-1234, reader.ReadInt16());
            Assert.Equal((short)-1234, reader.ReadInt16LE());
            Assert.Equal((ushort)0xBEEF, reader.ReadUInt16());
            Assert.Equal(0x11223344, reader.ReadInt32());
            Assert.Equal(0x11223344, reader.ReadInt32LE());
            Assert.Equal(0xCAFEBABE, reader.ReadUInt32());
            Assert.Equal(0x1122334455667788L, reader.ReadInt64());
            Assert.Equal(0xDEADBEEFCAFEBABEUL, reader.ReadUInt64());
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void Seek_BeyondCapacity_WithoutResize_Throws()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);

        var threw = false;

        try
        {
            writer.Seek(99, SeekOrigin.Begin);
        }
        catch (IOException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void Seek_End_ResolvesRelativeToBytesWritten()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);
        writer.Write((byte)0xAA);
        writer.Write((byte)0xBB);
        writer.Write((byte)0xCC);

        writer.Seek(0, SeekOrigin.Begin);
        Assert.Equal(0, writer.Position);

        writer.Seek(0, SeekOrigin.End);
        Assert.Equal(3, writer.Position);
    }

    [Fact]
    public void Seek_ForwardWithinCapacity_ZeroesOnlyNewGap()
    {
        Span<byte> backing = stackalloc byte[8];
        backing.Fill(0xCC);
        var writer = new SpanWriter(backing);
        writer.Write((byte)0xAB);
        writer.Seek(4, SeekOrigin.Begin);
        Assert.Equal(new byte[] { 0xAB, 0, 0, 0, 0xCC, 0xCC, 0xCC, 0xCC }, backing.ToArray());
    }

    [Fact]
    public void Seek_InvalidOrigin_DoesNotMoveCursor()
    {
        var writer = new SpanWriter(new byte[4]);
        writer.Write((byte)0xAB);
        var threw = false;

        try
        {
            writer.Seek(0, (SeekOrigin)99);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, writer.Position);
    }

    [Fact]
    public void Seek_OffsetOverflow_DoesNotMoveCursor()
    {
        var writer = new SpanWriter(new byte[4]);
        writer.Write((byte)0xAB);
        var threw = false;

        try
        {
            writer.Seek(int.MaxValue, SeekOrigin.Current);
        }
        catch (IOException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, writer.Position);
        Assert.Equal(new byte[] { 0xAB }, writer.Span.ToArray());
    }

    [Fact]
    public void Seek_ResizeFromEmptyCursor_ReachesRequestedCapacityAndZeroesGap()
    {
        Span<byte> backing = stackalloc byte[16];
        backing.Fill(0xCC);
        var writer = new SpanWriter(backing, true);

        try
        {
            writer.Seek(33, SeekOrigin.Begin);
            writer.Write((byte)0xAB);
            Assert.Equal(34, writer.BytesWritten);
            Assert.Equal(new byte[33], writer.Span[..33].ToArray());
            Assert.Equal((byte)0xAB, writer.Span[33]);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void ToArray_EmptyWriter_ReturnsEmpty()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);

        Assert.Empty(writer.ToArray());
    }

    [Fact]
    public void ToArray_ReturnsExactlyWrittenBytes()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);
        writer.Write((byte)1);
        writer.Write((byte)2);
        writer.Write((byte)3);

        var arr = writer.ToArray();

        Assert.Equal(new byte[] { 1, 2, 3 }, arr);
    }

    [Fact]
    public void ToSpan_Empty_ReturnsEmptyOwner()
    {
        var writer = new SpanWriter(8, true);

        var owner = writer.ToSpan();

        try
        {
            Assert.Equal(0, owner.Span.Length);
        }
        finally
        {
            owner.Dispose();
        }
    }

    [Fact]
    public void ToSpan_ExternalBuffer_CopiesPayloadAndResetsWriter()
    {
        Span<byte> backing = stackalloc byte[8];
        var writer = new SpanWriter(backing);
        writer.Write((byte)0x41);
        writer.Write((byte)0x42);
        using var owner = writer.ToSpan();
        backing.Fill(0xFF);
        writer.Dispose();
        Assert.Equal(new byte[] { 0x41, 0x42 }, owner.Span.ToArray());
        Assert.Equal(0, writer.Position);
        Assert.Equal(0, writer.BytesWritten);
    }

    [Fact]
    public void ToSpan_PooledBufferTransfersOwnershipToCaller()
    {
        var writer = new SpanWriter(8, true);
        writer.Write((byte)1);
        writer.Write((byte)2);
        writer.Write((byte)3);

        var owner = writer.ToSpan();

        try
        {
            Assert.Equal(3, owner.Span.Length);
            Assert.Equal(new byte[] { 1, 2, 3 }, owner.Span.ToArray());
        }
        finally
        {
            owner.Dispose();
        }
    }

    [Fact]
    public void WriteAscii_FixedLengthPadsWithNulls()
    {
        Span<byte> backing = stackalloc byte[8];
        var writer = new SpanWriter(backing);
        writer.WriteAscii("hi", 5);

        Assert.Equal(5, writer.Position);
        Assert.Equal((byte)'h', backing[0]);
        Assert.Equal((byte)'i', backing[1]);
        Assert.Equal(0, backing[2]);
        Assert.Equal(0, backing[3]);
        Assert.Equal(0, backing[4]);
    }

    [Fact]
    public void WriteAscii_FixedLengthTruncatesLongString()
    {
        Span<byte> backing = stackalloc byte[8];
        var writer = new SpanWriter(backing);
        writer.WriteAscii("hello world", 5);

        Assert.Equal(5, writer.Position);
        Assert.Equal("hello", Encoding.ASCII.GetString(backing[..5]));
    }

    [Fact]
    public void WriteAscii_StringRoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);
        writer.WriteAsciiNull("hello");

        var reader = new SpanReader(backing[..writer.Position]);
        Assert.Equal("hello", reader.ReadAscii());
    }

    [Theory, InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public void WriteAttribute_InsufficientCapacity_DoesNotModifyDestination(bool normalize, bool reverse)
    {
        byte[] buffer = [0x42, 0xCC, 0xCC, 0xCC];
        var writer = new SpanWriter(buffer);
        writer.WriteByte(0x42);
        var threw = false;

        try
        {
            writer.WriteAttribute(100, 50, normalize, reverse);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, writer.Position);
        Assert.Equal(1, writer.BytesWritten);
        Assert.Equal(new byte[] { 0x42, 0xCC, 0xCC, 0xCC }, buffer);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void WriteAttribute_LargeNormalizedValues_EncodePercentageWithoutOverflow(bool reverse)
    {
        Span<byte> buffer = stackalloc byte[4];
        var writer = new SpanWriter(buffer);

        writer.WriteAttribute(100_000_000, 50_000_000, true, reverse);

        Assert.Equal(
            reverse ? new byte[] { 0x00, 0x32, 0x00, 0x64 } : new byte[] { 0x00, 0x64, 0x00, 0x32 },
            writer.ToArray()
        );
    }

    [Fact]
    public void WriteBigUni_StringRoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[32];
        var writer = new SpanWriter(backing);
        writer.WriteBigUniNull("ciao");

        var reader = new SpanReader(backing[..writer.Position]);
        Assert.Equal("ciao", reader.ReadBigUni());
    }

    [Fact]
    public void WriteBool_EncodesAsByte()
    {
        Span<byte> backing = stackalloc byte[2];
        var writer = new SpanWriter(backing);

        writer.Write(true);
        writer.Write(false);

        Assert.Equal(1, backing[0]);
        Assert.Equal(0, backing[1]);
    }

    [Fact]
    public void WriteByte_AdvancesPositionAndBytesWritten()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);

        writer.Write((byte)0x42);
        writer.Write((byte)0x43);

        Assert.Equal(2, writer.Position);
        Assert.Equal(2, writer.BytesWritten);
        Assert.Equal(0x42, backing[0]);
        Assert.Equal(0x43, backing[1]);
    }

    [Fact]
    public void WriteBytes_InsufficientCapacity_LeavesDestinationUntouched()
    {
        var destination = new byte[] { 0xCC };
        var writer = new SpanWriter(destination);
        var exceptionThrown = false;

        try
        {
            writer.WriteBytes([0x01, 0x02]);
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Theory, InlineData("ABC", 2), InlineData("A\0", 2), InlineData("é", 2)]
    public void WriteFixedAscii_InvalidText_LeavesDestinationUntouched(string value, int width)
    {
        var destination = new byte[] { 0xCC, 0xCC };
        var writer = new SpanWriter(destination);

        var exceptionThrown = false;

        try
        {
            writer.WriteFixedAscii(value, width);
        }
        catch (ArgumentException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(new byte[] { 0xCC, 0xCC }, destination);
    }

    [Fact]
    public void WriteFixedAscii_NegativeWidth_LeavesDestinationUntouched()
    {
        var destination = new byte[] { 0xCC };
        var writer = new SpanWriter(destination);
        var exceptionThrown = false;

        try
        {
            writer.WriteFixedAscii("A", -1);
        }
        catch (ArgumentOutOfRangeException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal((byte)0xCC, destination[0]);
    }

    [Fact]
    public void WriteFixedAscii_PadsFieldWithoutTruncation()
    {
        Span<byte> destination = stackalloc byte[5];
        var writer = new SpanWriter(destination);

        writer.WriteFixedAscii("AB", 5);

        Assert.Equal(new byte[] { 0x41, 0x42, 0x00, 0x00, 0x00 }, destination.ToArray());
    }

    [Fact]
    public void WriteInt16_BigEndian()
    {
        Span<byte> backing = stackalloc byte[2];
        var writer = new SpanWriter(backing);
        writer.Write((short)0x1234);

        Assert.Equal(0x12, backing[0]);
        Assert.Equal(0x34, backing[1]);
    }

    [Fact]
    public void WriteInt32_BigEndian_RoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);
        writer.Write(0x12345678);

        var reader = new SpanReader(backing);
        Assert.Equal(0x12345678, reader.ReadInt32());
    }

    [Fact]
    public void WriteInt64_BigEndian_RoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[8];
        var writer = new SpanWriter(backing);
        writer.Write(0x1122334455667788L);

        var reader = new SpanReader(backing);
        Assert.Equal(0x1122334455667788L, reader.ReadInt64());
    }

    [Fact]
    public void WriteLE_Int16_LittleEndian()
    {
        Span<byte> backing = stackalloc byte[2];
        var writer = new SpanWriter(backing);
        writer.WriteLE((short)0x1234);

        Assert.Equal(0x34, backing[0]);
        Assert.Equal(0x12, backing[1]);
    }

    [Fact]
    public void WriteLE_Int32_RoundTripsThroughReaderLE()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);
        writer.WriteLE(0x12345678);

        var reader = new SpanReader(backing);
        Assert.Equal(0x12345678, reader.ReadInt32LE());
    }

    [Fact]
    public void WriteLittleUni_FixedLengthPadsWithUnicodeNulls()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);
        writer.WriteLittleUni("hi", 5);

        // Padding fills (5-2)*2 = 6 zero bytes after the 4 written bytes for "hi".
        Assert.Equal(10, writer.Position);
        Assert.Equal((byte)'h', backing[0]);
        Assert.Equal(0, backing[1]);
        Assert.Equal((byte)'i', backing[2]);
        Assert.Equal(0, backing[3]);

        for (var i = 4; i < 10; i++)
        {
            Assert.Equal(0, backing[i]);
        }
    }

    [Fact]
    public void WriteLittleUni_StringRoundTripsThroughReader_WithCustomEncoding()
    {
        // Use Moongate's TextEncoding.UnicodeLE to confirm GetTerminatorWidth handles
        // custom UnicodeEncoding subclasses correctly.
        Span<byte> backing = stackalloc byte[32];
        var writer = new SpanWriter(backing);
        writer.Write("test".AsSpan(), TextEncoding.UnicodeLE);
        writer.Write((ushort)0);

        var reader = new SpanReader(backing[..writer.Position]);
        Assert.Equal("test", reader.ReadString(TextEncoding.UnicodeLE));
    }

    [Fact]
    public void WriteNullTerminatedAscii_InsufficientCapacity_LeavesDestinationUntouched()
    {
        var destination = new byte[] { 0xCC, 0xCC };
        var writer = new SpanWriter(destination);

        var exceptionThrown = false;

        try
        {
            writer.WriteNullTerminatedAscii("AB");
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(new byte[] { 0xCC, 0xCC }, destination);
    }

    [Fact]
    public void WriteNullTerminatedAscii_ValidText_AppendsOneTerminator()
    {
        Span<byte> destination = stackalloc byte[3];
        var writer = new SpanWriter(destination);

        writer.WriteNullTerminatedAscii("AB");

        Assert.Equal(new byte[] { 0x41, 0x42, 0x00 }, destination.ToArray());
    }

    [Theory, InlineData(0), InlineData(1), InlineData(2)]
    public void WriteNullTerminated_InsufficientCapacity_IsAtomic(int encodingKind)
    {
        var backing = new byte[] { 0xCC, 0xCC };
        var writer = new SpanWriter(backing);
        var threw = false;

        try
        {
            switch (encodingKind)
            {
                case 0:
                    writer.WriteAsciiNull("AB");

                    break;
                case 1:
                    writer.WriteUTF8Null("é");

                    break;
                case 2:
                    writer.WriteBigUniNull("A");

                    break;
            }
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(0, writer.Position);
        Assert.Equal(new byte[] { 0xCC, 0xCC }, backing);
    }

    [Theory, InlineData(2), InlineData(65536)]
    public void WritePacketLength_InvalidFrameLength_DoesNotModifyBytes(int length)
    {
        var backing = Enumerable.Repeat((byte)0xCC, length).ToArray();
        var writer = new SpanWriter(backing);
        writer.Write(backing);
        var threw = false;

        try
        {
            writer.WritePacketLength();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(length, writer.Position);
        Assert.All(backing, value => Assert.Equal((byte)0xCC, value));
    }

    [Fact]
    public void WritePacketLength_PatchesHeaderAndPreservesCursor()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);
        writer.Write(new byte[] { 0xBD, 0, 0, 0x41 });
        writer.Seek(1, SeekOrigin.Begin);
        writer.WritePacketLength();
        Assert.Equal(new byte[] { 0xBD, 0, 4, 0x41 }, writer.Span.ToArray());
        Assert.Equal(1, writer.Position);
        Assert.Equal(4, writer.BytesWritten);
    }

    [Fact]
    public void WriteSByte_RoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[1];
        var writer = new SpanWriter(backing);
        writer.Write((sbyte)-128);

        var reader = new SpanReader(backing);
        Assert.Equal((sbyte)-128, reader.ReadSByte());
    }

    [Fact]
    public void WriteSpan_CopiesBytes()
    {
        Span<byte> backing = stackalloc byte[8];
        var writer = new SpanWriter(backing);
        ReadOnlySpan<byte> payload = [1, 2, 3, 4];

        writer.Write(payload);

        Assert.Equal(4, writer.Position);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, backing[..4].ToArray());
    }

    [Fact]
    public void WriteUInt32_BigEndian_RoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[4];
        var writer = new SpanWriter(backing);
        writer.Write(0xDEADBEEF);

        var reader = new SpanReader(backing);
        Assert.Equal(0xDEADBEEF, reader.ReadUInt32());
    }

    [Fact]
    public void WriteUTF8_StringRoundTripsThroughReader()
    {
        Span<byte> backing = stackalloc byte[32];
        var writer = new SpanWriter(backing);
        writer.WriteUTF8("héllo");
        writer.Write((byte)0);

        var reader = new SpanReader(backing[..writer.Position]);
        Assert.Equal("héllo", reader.ReadUTF8());
    }

    [Fact]
    public void Write_CharacterSourceInPooledBuffer_SurvivesGrowth()
    {
        var writer = new SpanWriter(16, true);

        try
        {
            var characters = new string('A', writer.Capacity / sizeof(char)).ToCharArray();
            var original = MemoryMarshal.AsBytes(characters.AsSpan()).ToArray();
            writer.Write(original);
            writer.Write(MemoryMarshal.Cast<byte, char>(writer.RawBuffer), Encoding.ASCII);
            Assert.Equal(original, writer.Span[..original.Length].ToArray());
            Assert.Equal(
                Enumerable.Repeat((byte)0x41, characters.Length).ToArray(),
                writer.Span[original.Length..].ToArray()
            );
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void Write_FixedUnicodeLengthOverflow_DoesNotModifyDestination()
    {
        var backing = Enumerable.Repeat((byte)0xCC, 8).ToArray();
        var writer = new SpanWriter(backing);
        var threw = false;

        try
        {
            writer.Write("A".AsSpan(), Encoding.Unicode, int.MaxValue);
        }
        catch (OverflowException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(0, writer.Position);
        Assert.All(backing, value => Assert.Equal((byte)0xCC, value));
    }

    [Fact]
    public void Write_FixedUtf32FieldWithoutPaddingCapacity_DoesNotModifyDestination()
    {
        byte[] buffer = [0x42, 0xCC, 0xCC, 0xCC, 0xCC];
        var writer = new SpanWriter(buffer);
        writer.WriteByte(0x42);
        var threw = false;

        try
        {
            writer.Write("\U0001F600".AsSpan(), Encoding.UTF32, 2);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, writer.Position);
        Assert.Equal(1, writer.BytesWritten);
        Assert.Equal(new byte[] { 0x42, 0xCC, 0xCC, 0xCC, 0xCC }, buffer);
    }

    [Fact]
    public void Write_FixedUtf8FieldTooNarrow_DoesNotModifyDestination()
    {
        byte[] buffer = [0x42, 0xCC, 0xCC, 0xCC];
        var writer = new SpanWriter(buffer);
        writer.WriteByte(0x42);
        var threw = false;

        try
        {
            writer.Write("é".AsSpan(), Encoding.UTF8, 1);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        Assert.True(threw);
        Assert.Equal(1, writer.Position);
        Assert.Equal(1, writer.BytesWritten);
        Assert.Equal(new byte[] { 0x42, 0xCC, 0xCC, 0xCC }, buffer);
    }

    [Fact]
    public void Write_FixedUtf8Field_UsesEncodedByteWidth()
    {
        Span<byte> buffer = stackalloc byte[2];
        var writer = new SpanWriter(buffer);

        writer.Write("é".AsSpan(), Encoding.UTF8, 2);

        Assert.Equal(new byte[] { 0xC3, 0xA9 }, writer.ToArray());
        var reader = new SpanReader(writer.Span);
        Assert.Equal("é", reader.ReadString(Encoding.UTF8, fixedLength: 2));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Write_OverlappingCharactersDuringTranscoding_PreservesSource()
    {
        Span<byte> backing = stackalloc byte[16];
        var writer = new SpanWriter(backing);
        var characters = new[] { '\u20AC', '\u20AC', '\u20AC', '\u20AC' };
        writer.Write(MemoryMarshal.AsBytes(characters.AsSpan()));
        writer.Seek(0, SeekOrigin.Begin);
        writer.Write(MemoryMarshal.Cast<byte, char>(writer.RawBuffer[..8]), Encoding.UTF8);
        Assert.Equal(Convert.FromHexString("E282ACE282ACE282ACE282AC"), writer.Span.ToArray());
    }

    [Fact]
    public void Write_OverlappingExternalSlice_CopiesSourceBeforeDestination()
    {
        var backing = new byte[] { 1, 2, 3, 4, 5 };
        var writer = new SpanWriter(backing.AsSpan(1, 4));
        writer.Write(backing.AsSpan(0, 3));
        Assert.Equal(new byte[] { 1, 1, 2, 3, 5 }, backing);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Write_OwnBufferWithGrowth_PreservesCopiedBytes(bool usePacketAlias)
    {
        var writer = new SpanWriter(2, true);

        try
        {
            var count = writer.Capacity;

            for (var i = 0; i < count; i++)
            {
                writer.Write((byte)0xAB);
            }

            if (usePacketAlias)
            {
                writer.WriteBytes(writer.Span);
            }
            else
            {
                writer.Write(writer.Span);
            }
            Assert.Equal(count * 2, writer.BytesWritten);
            Assert.All(writer.ToArray(), value => Assert.Equal((byte)0xAB, value));
        }
        finally
        {
            writer.Dispose();
        }
    }
}
