using System.Net;
using SquidStd.Network.Spans;

namespace Moongate.Network.Helpers;

/// <summary>
/// Writes/reads an IPv4 address as 4 octets in reverse order, the byte order UO
/// clients expect in the server list and game-server redirect packets.
/// </summary>
public static class IpV4Writer
{
    public static void WriteReversed(ref SpanWriter writer, IPAddress address)
    {
        var octets = address.MapToIPv4().GetAddressBytes();

        writer.Write(octets[3]);
        writer.Write(octets[2]);
        writer.Write(octets[1]);
        writer.Write(octets[0]);
    }

    public static IPAddress ReadReversed(ref SpanReader reader)
    {
        var d = reader.ReadByte();
        var c = reader.ReadByte();
        var b = reader.ReadByte();
        var a = reader.ReadByte();

        return new IPAddress(new[] { a, b, c, d });
    }
}
