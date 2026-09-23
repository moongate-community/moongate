using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class StubPacketSendService : IPacketSendService
{
    public int SentCount { get; private set; }
    public INetworkConnection? ExpectedConnection { get; private set; }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    public Task DisconnectAsync(long sessionId)
        => Task.CompletedTask;

    public bool TrySend(long sessionId, IOutgoingPacket packet)
    {
        SentCount++;
        return true;
    }

    public bool TrySend(long sessionId, INetworkConnection expectedConnection, IOutgoingPacket packet)
    {
        ExpectedConnection = expectedConnection;
        return TrySend(sessionId, packet);
    }
}
