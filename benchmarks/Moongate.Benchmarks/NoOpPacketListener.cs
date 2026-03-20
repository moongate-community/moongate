using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Session;

namespace Moongate.Benchmarks;

public sealed class NoOpPacketListener : IPacketListener
{
    public Task<bool> HandlePacketAsync(IGameSession session, IGameNetworkPacket packet)
    {
        _ = session;
        _ = packet;

        return Task.FromResult(true);
    }
}
