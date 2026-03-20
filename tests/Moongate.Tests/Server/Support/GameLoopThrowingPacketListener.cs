using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Session;

namespace Moongate.Tests.Server.Support;

public sealed class GameLoopThrowingPacketListener : IPacketListener
{
    public Task<bool> HandlePacketAsync(IGameSession session, IGameNetworkPacket packet)
        => throw new InvalidOperationException("listener failure");
}
