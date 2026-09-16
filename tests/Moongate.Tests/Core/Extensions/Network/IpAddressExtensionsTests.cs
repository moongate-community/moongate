using System.Net;
using Moongate.Core.Extensions.Network;

namespace Moongate.Tests.Core.Extensions.Network;

public sealed class IpAddressExtensionsTests
{
    [Theory,
     InlineData("0.0.0.0", 0u),
     InlineData("127.0.0.1", 0x0100007Fu),
     InlineData("192.168.1.2", 0x0201A8C0u),
     InlineData("255.255.255.255", uint.MaxValue),
     InlineData("::ffff:192.168.1.2", 0x0201A8C0u)]
    public void ToRawAddress_UsesIpv4BytesInLittleEndianOrder(string address, uint expected)
    {
        var ip = IPAddress.Parse(address);

        Assert.Equal(expected, ip.ToRawAddress());
        Assert.Equal(expected, new IPEndPoint(ip, 2593).ToRawAddress());
    }
}
