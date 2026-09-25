using System.Net;
using Moongate.Network.Packets.Data.Login;

namespace Moongate.Network.Packets.Tests.Data.Login;

public class GameServerEntryTests
{
    [Fact]
    public void Constructor_IPv6Address_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new GameServerEntry(1, "Shard", 0, 0, IPAddress.IPv6Loopback));
    }

    [Theory, InlineData("123456789012345678901234567890123"), InlineData("Shardé"), InlineData("Shard\0")]
    public void Constructor_InvalidName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => new GameServerEntry(1, name, 0, 0, IPAddress.Loopback));
    }
}
