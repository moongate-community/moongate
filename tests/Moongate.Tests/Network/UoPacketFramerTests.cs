using Moongate.Network.Exceptions;
using Moongate.Network.Framing;

namespace Moongate.Tests.Network;

public class UoPacketFramerTests
{
    private readonly UoPacketFramer _framer = new();

    [Fact]
    public void TryReadFrame_EmptyBuffer_NeedsMoreBytes()
    {
        Assert.False(_framer.TryReadFrame([], out _));
    }

    [Fact]
    public void TryReadFrame_CompleteFixedPacket_ReturnsItsLength()
    {
        var moveRequest = new byte[] { 0x02, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00 };

        Assert.True(_framer.TryReadFrame(moveRequest, out var length));
        Assert.Equal(7, length);
    }

    [Fact]
    public void TryReadFrame_PartialFixedPacket_NeedsMoreBytes()
    {
        var partial = new byte[] { 0x02, 0x01, 0x05 };

        Assert.False(_framer.TryReadFrame(partial, out _));
    }

    [Fact]
    public void TryReadFrame_CoalescedPackets_ReturnsOnlyFirstFrame()
    {
        // ping (2 bytes) followed by move request (7 bytes)
        var buffer = new byte[] { 0x73, 0x00, 0x02, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00 };

        Assert.True(_framer.TryReadFrame(buffer, out var length));
        Assert.Equal(2, length);
    }

    [Fact]
    public void TryReadFrame_CompleteVariablePacket_ReadsLengthFromHeader()
    {
        // client version 0xBD: id + big-endian ushort length + payload
        var buffer = new byte[] { 0xBD, 0x00, 0x06, (byte)'7', (byte)'.', 0x00 };

        Assert.True(_framer.TryReadFrame(buffer, out var length));
        Assert.Equal(6, length);
    }

    [Fact]
    public void TryReadFrame_VariablePacketHeaderIncomplete_NeedsMoreBytes()
    {
        Assert.False(_framer.TryReadFrame([0xBD, 0x00], out _));
    }

    [Fact]
    public void TryReadFrame_VariablePacketBodyIncomplete_NeedsMoreBytes()
    {
        Assert.False(_framer.TryReadFrame([0xBD, 0x00, 0x10, 0x01], out _));
    }

    [Fact]
    public void TryReadFrame_UnknownPacketId_Throws()
    {
        Assert.Throws<UoFramingException>(() => _framer.TryReadFrame([0xE0, 0x00, 0x00], out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TryReadFrame_VariableLengthBelowHeaderSize_Throws(int declared)
    {
        var buffer = new byte[] { 0xBD, 0x00, (byte)declared, 0x00 };

        Assert.Throws<UoFramingException>(() => _framer.TryReadFrame(buffer, out _));
    }
}
