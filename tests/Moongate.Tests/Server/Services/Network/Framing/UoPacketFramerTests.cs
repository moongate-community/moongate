using Moongate.Network.Packets.Registry;
using Moongate.Server.Services.Network.Framing;

namespace Moongate.Tests.Server.Services.Network.Framing;

public sealed class UoPacketFramerTests
{
    private static readonly byte[] ClientVersion = Convert.FromHexString("BD000C372E302E3130392E30");

    [Theory, InlineData(0), InlineData(-1), InlineData(65536)]
    public void Constructor_InvalidMaximumFrameLength_ThrowsArgumentOutOfRangeException(int maxFrameLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UoPacketFramer(PacketRegistry.Default, maxFrameLength)
        );
    }

    [Fact]
    public void Constructor_MutableRegistry_ThrowsArgumentException()
    {
        var registry = new PacketRegistry();

        Assert.Throws<ArgumentException>(() => new UoPacketFramer(registry));
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new UoPacketFramer(null!));
    }

    [Fact]
    public void TryReadFrame_CompleteFixedPacket_ReturnsPacketLength()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        Assert.True(framer.TryReadFrame(new byte[] { 0x73, 0x2A }, out var frameLength));
        Assert.Equal(2, frameLength);
    }

    [Fact]
    public void TryReadFrame_CompleteVariablePacket_ReturnsDeclaredLength()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        Assert.True(framer.TryReadFrame(ClientVersion, out var frameLength));
        Assert.Equal(12, frameLength);
    }

    [Fact]
    public void TryReadFrame_DoesNotModifyInput()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);
        var packet = ClientVersion.ToArray();
        var original = packet.ToArray();

        Assert.True(framer.TryReadFrame(packet, out _));
        Assert.Equal(original, packet);
    }

    [Fact]
    public void TryReadFrame_EveryIncompleteFixedSplit_ReturnsFalseAndZeroLength()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);
        byte[] packet = [0x73, 0x2A];

        for (var length = 0; length < packet.Length; length++)
        {
            Assert.False(framer.TryReadFrame(packet.AsSpan(0, length), out var frameLength));
            Assert.Equal(0, frameLength);
        }
    }

    [Fact]
    public void TryReadFrame_EveryIncompleteVariableSplit_ReturnsFalseAndZeroLength()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        for (var length = 0; length < ClientVersion.Length; length++)
        {
            Assert.False(framer.TryReadFrame(ClientVersion.AsSpan(0, length), out var frameLength));
            Assert.Equal(0, frameLength);
        }
    }

    [Fact]
    public void TryReadFrame_FixedLengthAboveConfiguredMaximum_ThrowsBeforePayloadArrives()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default, 2);

        Assert.Throws<InvalidDataException>(() => framer.TryReadFrame(new byte[] { 0x80 }, out _));
    }

    [Fact]
    public void TryReadFrame_MultipleFrames_ReturnsOnlyFirstFrameLength()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);
        byte[] packets = [0x73, 0x2A, 0x73, 0x2B];

        Assert.True(framer.TryReadFrame(packets, out var frameLength));
        Assert.Equal(2, frameLength);
    }

    [Fact]
    public void TryReadFrame_SharedOpcode_UsesIncomingVariableDescriptor()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        Assert.False(framer.TryReadFrame(ClientVersion.AsSpan(0, 3), out var incompleteLength));
        Assert.Equal(0, incompleteLength);
        Assert.True(framer.TryReadFrame(ClientVersion, out var frameLength));
        Assert.Equal(12, frameLength);
    }

    [Theory, InlineData("99"), InlineData("55")]
    public void TryReadFrame_UnknownOrOutgoingOnlyOpcode_ThrowsInvalidDataException(string hex)
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        Assert.Throws<InvalidDataException>(() => framer.TryReadFrame(Convert.FromHexString(hex), out _));
    }

    [Theory, InlineData("2A", 0), InlineData("2A010203", 3)]
    public void TryReadFrame_UnregisteredOpcode_ReportsBufferedBytesAfterOpcode(string hex, int bufferedBytes)
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        var exception = Assert.Throws<InvalidDataException>(() => framer.TryReadFrame(Convert.FromHexString(hex), out _));

        Assert.Contains("0x2A", exception.Message);
        Assert.Contains($"{bufferedBytes} bytes buffered after opcode", exception.Message);
    }

    [Fact]
    public void TryReadFrame_VariableLengthAboveConfiguredMaximum_ThrowsInvalidDataException()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default, 11);

        Assert.Throws<InvalidDataException>(() => framer.TryReadFrame(ClientVersion.AsSpan(0, 3), out _));
    }

    [Fact]
    public void TryReadFrame_VariableLengthBelowMinimum_ThrowsInvalidDataException()
    {
        var framer = new UoPacketFramer(PacketRegistry.Default);

        Assert.Throws<InvalidDataException>(() => framer.TryReadFrame(Convert.FromHexString("BD0002"), out _));
    }
}
