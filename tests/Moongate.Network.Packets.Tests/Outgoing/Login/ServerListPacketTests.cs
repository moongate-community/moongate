using System.Net;
using Moongate.Network.Packets.Data.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Outgoing.Login;

public class ServerListPacketTests
{
    [Fact]
    public void Constructor_MaximumCountAcceptedAndOneMoreRejected()
    {
        var entry = new GameServerEntry(1, "A", 0, 0, IPAddress.Loopback);

        var maximum = new ServerListPacket(Enumerable.Repeat(entry, 1638));

        Assert.Equal(65526, maximum.Length);
        Assert.Throws<ArgumentException>(() => new ServerListPacket(Enumerable.Repeat(entry, 1639)));
    }

    [Fact]
    public void Constructor_NullEntry_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => new ServerListPacket(new GameServerEntry[] { null! }));

    [Fact]
    public void Constructor_SnapshotsCollectionEntriesAndAddressBytes()
    {
        var addressBytes = new byte[] { 1, 2, 3, 4 };
        var source = new List<GameServerEntry>
        {
            new(1, "A", 25, 0, new(addressBytes))
        };
        var packet = new ServerListPacket(source);
        source.Clear();
        addressBytes[0] = 9;

        Assert.Single(packet.Servers);
        Assert.Equal(Convert.FromHexString("04030201"), PacketCodec.Encode(packet)[42..46]);
    }

    [Fact]
    public void Encode_MultipleEntries_MatchesCompleteIndependentFixture()
    {
        var packet = new ServerListPacket(
            [
                new(1, "A", 0, 1, IPAddress.Parse("1.2.3.4")),
                new(2, "12345678901234567890123456789012", 100, -8, IPAddress.Parse("10.20.30.40"))
            ]
        );
        var expected = Convert.FromHexString(
            "A800565D0002" +
            "000141" +
            "00000000000000000000000000000000" +
            "000000000000000000000000000000" +
            "000104030201" +
            "0002" +
            "31323334353637383930" +
            "31323334353637383930" +
            "31323334353637383930" +
            "3132" +
            "64F8281E140A"
        );

        var bytes = PacketCodec.Encode(packet);

        Assert.Equal(86, bytes.Length);
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Encode_OneEntry_MatchesReversedIpFixture()
    {
        var entry = new GameServerEntry(0x0102, "Shard", 50, -2, IPAddress.Parse("192.168.0.206"));
        var packet = new ServerListPacket([entry]);
        var expected = Convert.FromHexString(
            "A8002E5D000101025368617264" + "00000000000000000000000000000000" + "0000000000000000000000" + "32FECE00A8C0"
        );

        Assert.Equal(46, packet.Length);
        Assert.Single(packet.Servers);
        Assert.Equal(expected, PacketCodec.Encode(packet));
        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }
}
