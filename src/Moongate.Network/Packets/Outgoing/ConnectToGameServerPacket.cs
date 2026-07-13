using System.Net;
using Moongate.Network.Helpers;
using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Connect to game server (0x8C): redirects the client to the game port with an auth key.</summary>
public readonly record struct ConnectToGameServerPacket(IPAddress Address, ushort Port, uint AuthKey) : IOutgoingPacket
{
    public const byte PacketId = 0x8C;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        IpV4Writer.WriteNormal(ref writer, Address);
        writer.Write(Port);
        writer.Write(AuthKey);
    }
}
