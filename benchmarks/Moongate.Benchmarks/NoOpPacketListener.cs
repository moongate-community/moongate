using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Listener;

namespace Moongate.Benchmarks;

public sealed class NoOpPacketListener : IPacketListener
{
    public Task<bool> HandlePacketAsync(GameSession session, IGameNetworkPacket packet)
    {
        _ = session;
        _ = packet;

        return Task.FromResult(true);
    }
}
