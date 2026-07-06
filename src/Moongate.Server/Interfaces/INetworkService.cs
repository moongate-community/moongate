using Moongate.Network.Interfaces;

namespace Moongate.Server.Interfaces;

/// <summary>Owns the session-aware packet dispatch table and exposes the bound listener port.</summary>
public interface INetworkService
{
    /// <summary>The bound TCP port, or 0 before the listener starts.</summary>
    int Port { get; }

    /// <summary>Registers <paramref name="handler" /> for its packet's opcode (<c>TPacket.PacketId</c>).</summary>
    void RegisterHandler<TPacket>(IPacketHandler<TPacket> handler) where TPacket : IIncomingPacket<TPacket>;
}
