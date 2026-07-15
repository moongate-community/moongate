using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Client version (0xBD): the client answers the server's version request with its build string
/// (e.g. "7.0.115.0"), null-terminated ASCII. Variable length: 3-byte header + string.
/// </summary>
public readonly record struct ClientVersionPacket(string Version) : IIncomingPacket<ClientVersionPacket>
{
    public static byte PacketId => 0xBD;

    public static ClientVersionPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();                // packet id
        var length = reader.ReadUInt16(); // full packet length
        var payloadLength = length - 3;

        var version = payloadLength <= 0 ? string.Empty : reader.ReadAscii(payloadLength).TrimEnd('\0');

        return new(version);
    }
}
