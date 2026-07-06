using Moongate.Network.Interfaces;

namespace Moongate.Server.Interfaces;

/// <summary>Owns the TCP listener, the session-aware dispatch table, and the seed handshake.</summary>
public interface INetworkService
{
    /// <summary>Registers <paramref name="handler" /> for its packet's opcode (<c>TPacket.PacketId</c>).</summary>
    void RegisterHandler<TPacket>(IPacketHandler<TPacket> handler) where TPacket : IIncomingPacket<TPacket>;

    Task StartAsync();

    Task StopAsync();

    int Port { get; }
}
