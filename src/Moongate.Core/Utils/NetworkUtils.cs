using System.Net;
using System.Net.NetworkInformation;

namespace Moongate.Core.Utils;

/// <summary>Enumerates local network addresses and builds endpoints for server address listings.</summary>
public static class NetworkUtils
{
    /// <summary>Gets the unicast IPv4 and IPv6 addresses reported by all local network interfaces.</summary>
    /// <remarks>
    /// Includes loopback addresses and does not filter interfaces by operational status or remove duplicates.
    /// </remarks>
    public static IEnumerable<IPAddress> GetLocalIpAddresses()
        => NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address);

    /// <summary>Gets local endpoints with the supplied endpoint's address family and port.</summary>
    /// <param name="endPoint">Provides the IPv4 or IPv6 family and the port to use.</param>
    /// <remarks>
    /// Preserves the legacy address-listing behavior: the supplied address selects only the address family.
    /// This method does not bind sockets or check whether the returned endpoints are actually listening.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The endpoint is null.</exception>
    public static IEnumerable<IPEndPoint> GetListeningAddresses(IPEndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        var addressFamily = endPoint.AddressFamily;
        var port = endPoint.Port;

        return GetLocalIpAddresses()
            .Where(address => address.AddressFamily == addressFamily)
            .Select(address => new IPEndPoint(address, port));
    }
}
