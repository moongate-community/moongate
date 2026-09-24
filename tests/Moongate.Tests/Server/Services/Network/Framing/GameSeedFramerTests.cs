using Moongate.Network.Packets.Registry;
using Moongate.Server.Services.Network.Framing;

namespace Moongate.Tests.Server.Services.Network.Framing;

public sealed class GameSeedFramerTests
{
    [Theory, InlineData(1), InlineData(2), InlineData(3)]
    public void TryReadFrame_FragmentedRawSeed_WaitsForFourthByte(int length)
    {
        var framer = new GameSeedFramer(PacketRegistry.Default);
        byte[] seed = [0x73, 0x12, 0x34, 0x56];

        Assert.False(framer.TryReadFrame(seed.AsSpan(0, length), out var frameLength));
        Assert.Equal(0, frameLength);
        Assert.True(framer.TryReadFrame(seed, out frameLength));
        Assert.Equal(4, frameLength);
    }

    [Fact]
    public void TryReadFrame_RawSeedAndGameLoginInOneBuffer_SeparatesFrames()
    {
        var framer = new GameSeedFramer(PacketRegistry.Default);
        byte[] bytes = new byte[69];
        bytes[0] = 0x73;
        bytes[1] = 0x12;
        bytes[2] = 0x34;
        bytes[3] = 0x56;
        bytes[4] = 0x91;

        Assert.True(framer.TryReadFrame(bytes, out var seedLength));
        Assert.Equal(4, seedLength);
        Assert.True(framer.TryReadFrame(bytes.AsSpan(seedLength), out var packetLength));
        Assert.Equal(65, packetLength);
    }

    [Fact]
    public void TryReadFrame_SeparateConnections_DoNotShareSeedState()
    {
        var first = new GameSeedFramer(PacketRegistry.Default);
        var second = new GameSeedFramer(PacketRegistry.Default);
        byte[] seed = [0x73, 0x12, 0x34, 0x56];

        Assert.True(first.TryReadFrame(seed, out var firstLength));
        Assert.Equal(4, firstLength);
        Assert.False(second.TryReadFrame(seed.AsSpan(0, 3), out var secondLength));
        Assert.Equal(0, secondLength);
        Assert.True(second.TryReadFrame(seed, out secondLength));
        Assert.Equal(4, secondLength);
    }

    [Fact]
    public void TryReadFrame_DirectVersionSeed_RemainsAnOrdinaryPacket()
    {
        var framer = new GameSeedFramer(PacketRegistry.Default);
        byte[] bytes = new byte[23];
        bytes[0] = 0xEF;
        bytes[1] = 0x11;
        bytes[21] = 0x73;
        bytes[22] = 0x2A;

        Assert.False(framer.TryReadFrame(bytes.AsSpan(0, 4), out var incompleteLength));
        Assert.Equal(0, incompleteLength);
        Assert.True(framer.TryReadFrame(bytes, out var seedPacketLength));
        Assert.Equal(21, seedPacketLength);
        Assert.True(framer.TryReadFrame(bytes.AsSpan(seedPacketLength), out var pingLength));
        Assert.Equal(2, pingLength);
    }

    [Fact]
    public void TryReadFrame_ZeroRawSeed_RejectsConnection()
    {
        var framer = new GameSeedFramer(PacketRegistry.Default);

        Assert.Throws<InvalidDataException>(() => { framer.TryReadFrame(new byte[4], out _); });
    }

    [Fact]
    public void TryReadFrame_MalformedPacketAfterSeed_RejectsConnection()
    {
        var framer = new GameSeedFramer(PacketRegistry.Default);
        byte[] seed = [0x73, 0x12, 0x34, 0x56];
        Assert.True(framer.TryReadFrame(seed, out _));

        Assert.Throws<InvalidDataException>(() => { framer.TryReadFrame(new byte[] { 0x2A }, out _); });
    }
}
