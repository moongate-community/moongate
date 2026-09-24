using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class StubPacketSendService : IPacketSendService
{
    public int SentCount { get; private set; }
    public INetworkConnection? ExpectedConnection { get; private set; }
    public bool RejectTerminalSend { get; init; }

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

    public async Task<bool> SendAndDisconnectAsync(
        long sessionId,
        INetworkConnection expectedConnection,
        IOutgoingPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (RejectTerminalSend)
        {
            return false;
        }

        TrySend(sessionId, expectedConnection, packet);
        await expectedConnection.CloseAsync(cancellationToken);

        return true;
    }
}
