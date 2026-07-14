using System.Net;
using SquidStd.Network.Spans;

namespace Moongate.Network.Helpers;

/// <summary>
/// Writes/reads an IPv4 address as 4 octets. UO uses opposite byte orders in the
/// two login packets: the server list (0xA8) expects the octets reversed, while
/// the game-server redirect (0x8C) expects them in normal order.
/// </summary>
public static class IpV4Writer
{
    public static IPAddress ReadReversed(ref SpanReader reader)
    {
        var d = reader.ReadByte();
        var c = reader.ReadByte();
        var b = reader.ReadByte();
        var a = reader.ReadByte();

        return new(new[] { a, b, c, d });
    }

    public static void WriteNormal(ref SpanWriter writer, IPAddress address)
    {
        var octets = address.MapToIPv4().GetAddressBytes();

        writer.Write(octets[0]);
        writer.Write(octets[1]);
        writer.Write(octets[2]);
        writer.Write(octets[3]);
    }

    public static void WriteReversed(ref SpanWriter writer, IPAddress address)
    {
        var octets = address.MapToIPv4().GetAddressBytes();

        writer.Write(octets[3]);
        writer.Write(octets[2]);
        writer.Write(octets[1]);
        writer.Write(octets[0]);
    }
}
