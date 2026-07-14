using Moongate.Core.Extensions;
using Moongate.Core.Types;
using Moongate.Network.Packets.Incoming;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class IncomingPacketsTests
{
    [Fact]
    public void AccountLoginRequestPacket_Read_ParsesCredentials()
    {
        var bytes = new byte[62];
        bytes[0] = 0x80;
        "squid"u8.CopyTo(bytes.AsSpan(1));   // account, ascii[30] zero-padded
        "secret"u8.CopyTo(bytes.AsSpan(31)); // password, ascii[30] zero-padded
        bytes[61] = 0x5D;                    // next login key

        var reader = new SpanReader(bytes);
        var packet = AccountLoginRequestPacket.Read(ref reader);

        Assert.Equal("squid", packet.Account);
        Assert.Equal("secret", packet.Password);
        Assert.Equal(0x5D, packet.NextLoginKey);
    }

    [Fact]
    public void LoginSeedPacket_Read_ParsesSeedAndVersion()
    {
        var bytes = new byte[21];
        bytes[0] = 0xEF;
        bytes[4] = 0x2A; // seed = 42 (big-endian uint at 1..4)
        bytes[8] = 7;    // major
        bytes[12] = 0;   // minor
        bytes[16] = 15;  // revision
        bytes[20] = 1;   // prototype

        var reader = new SpanReader(bytes);
        var packet = LoginSeedPacket.Read(ref reader);

        Assert.Equal(42u, packet.Seed);
        Assert.Equal(7u, packet.Major);
        Assert.Equal(0u, packet.Minor);
        Assert.Equal(15u, packet.Revision);
        Assert.Equal(1u, packet.Prototype);
    }

    [Fact]
    public void MoveRequestPacket_Read_ParsesDirectionSequenceAndKey()
    {
        var bytes = new byte[] { 0x02, 0x81, 0x0A, 0x00, 0x00, 0x00, 0x2A };

        var reader = new SpanReader(bytes);
        var packet = MoveRequestPacket.Read(ref reader);

        Assert.Equal(DirectionType.NorthEast | DirectionType.Running, packet.Direction);
        Assert.True(packet.Direction.IsRunning());
        Assert.Equal(DirectionType.NorthEast, packet.Direction.StripRunning());
        Assert.Equal(0x0A, packet.Sequence);
        Assert.Equal(42u, packet.FastwalkKey);
    }
}
