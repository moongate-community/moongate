using System.Net;
using System.Net.Sockets;
using Moongate.Core.Utils;

namespace Moongate.Tests.Integration.Core.Utils;

public sealed class NetworkUtilsTests
{
    [Theory, InlineData("0.0.0.0", 0), InlineData("0.0.0.0", 2593), InlineData("192.0.2.10", 65535)]
    public void GetListeningAddresses_Ipv4Endpoint_UsesFamilyAndPreservesPort(string address, int port)
    {
        var endPoints = NetworkUtils.GetListeningAddresses(new(IPAddress.Parse(address), port)).ToArray();

        Assert.Contains(new(IPAddress.Loopback, port), endPoints);
        Assert.All(
            endPoints,
            endPoint =>
            {
                Assert.Equal(AddressFamily.InterNetwork, endPoint.AddressFamily);
                Assert.Equal(port, endPoint.Port);
                Assert.NotEqual(IPAddress.Any, endPoint.Address);
            }
        );
    }

    [Fact]
    public void GetListeningAddresses_Ipv6Endpoint_DoesNotIncludeIpv4Addresses()
    {
        var endPoints = NetworkUtils.GetListeningAddresses(new(IPAddress.IPv6Any, 2593)).ToArray();

        // IPv6 may be disabled on the host; an empty result is valid in that case.
        Assert.All(
            endPoints,
            endPoint =>
            {
                Assert.Equal(AddressFamily.InterNetworkV6, endPoint.AddressFamily);
                Assert.Equal(2593, endPoint.Port);
                Assert.NotEqual(IPAddress.IPv6Any, endPoint.Address);
            }
        );
    }

    [Fact]
    public void GetListeningAddresses_NullEndpoint_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => NetworkUtils.GetListeningAddresses(null!));
    }

    [Fact]
    public void GetLocalIpAddresses_IncludesLoopbackAndExcludesWildcardAddresses()
    {
        var addresses = NetworkUtils.GetLocalIpAddresses().ToArray();

        Assert.Contains(IPAddress.Loopback, addresses);
        Assert.DoesNotContain(IPAddress.Any, addresses);
        Assert.DoesNotContain(IPAddress.IPv6Any, addresses);
        Assert.All(
            addresses,
            address =>
                Assert.True(address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
        );
    }
}
