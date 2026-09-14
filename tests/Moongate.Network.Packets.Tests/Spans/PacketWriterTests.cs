using Moongate.Core.Primitives;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Spans;

public class PacketWriterTests
{
    [Fact]
    public void NumericWrites_KnownValues_UseExplicitEndianOrder()
    {
        Span<byte> destination = stackalloc byte[14];
        var writer = new PacketWriter(destination);

        writer.WriteUInt16BigEndian(0x1234);
        writer.WriteUInt32BigEndian(0x12345678);
        writer.WriteUInt32LittleEndian(0x12345678);
        writer.WriteSerial(new Serial(0x40000001));

        Assert.Equal(Convert.FromHexString("1234123456787856341240000001"), writer.WrittenSpan.ToArray());
        Assert.Equal(14, writer.WrittenCount);
        Assert.Equal(0, writer.Remaining);
    }

    [Fact]
    public void FailedPrimitiveWrite_LeavesCountAndDestinationUnchanged()
    {
        var destination = new byte[] { 0xCC, 0xCC, 0xCC };
        var writer = new PacketWriter(destination);

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
    public void EnsureCapacity_NegativeCount_ThrowsWithoutAdvancing()
    {
        Span<byte> destination = stackalloc byte[1];
        var writer = new PacketWriter(destination);

        var exceptionThrown = false;
        try
        {
            writer.EnsureCapacity(-1);
        }
        catch (ArgumentOutOfRangeException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void WriteFixedAscii_PadsFieldWithoutTruncation()
    {
        Span<byte> destination = stackalloc byte[5];
        var writer = new PacketWriter(destination);

        writer.WriteFixedAscii("AB", 5);

        Assert.Equal(new byte[] { 0x41, 0x42, 0x00, 0x00, 0x00 }, destination.ToArray());
    }

    [Theory]
    [InlineData("ABC", 2)]
    [InlineData("A\0", 2)]
    [InlineData("é", 2)]
    public void WriteFixedAscii_InvalidText_LeavesDestinationUntouched(string value, int width)
    {
        var destination = new byte[] { 0xCC, 0xCC };
        var writer = new PacketWriter(destination);

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
        var writer = new PacketWriter(destination);
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
    public void WriteBytes_InsufficientCapacity_LeavesDestinationUntouched()
    {
        var destination = new byte[] { 0xCC };
        var writer = new PacketWriter(destination);
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

    [Fact]
    public void WriteNullTerminatedAscii_InsufficientCapacity_LeavesDestinationUntouched()
    {
        var destination = new byte[] { 0xCC, 0xCC };
        var writer = new PacketWriter(destination);

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
        var writer = new PacketWriter(destination);

        writer.WriteNullTerminatedAscii("AB");

        Assert.Equal(new byte[] { 0x41, 0x42, 0x00 }, destination.ToArray());
    }
}
