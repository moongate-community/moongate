using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class StubPacketSendService : IPacketSendService
{
    private readonly HashSet<Type> _ignored = [];

    public int SentCount { get; private set; }

    /// <summary>
    ///     Gets every packet handed to TrySend, in order.
    /// </summary>
    public List<IOutgoingPacket> Sent { get; } = [];
    public List<long> SentSessionIds { get; } = [];

    /// <summary>
    ///     Gets the packets of the ignored types, in order, kept apart from <see cref="Sent" />.
    /// </summary>
    public List<IOutgoingPacket> Ignored { get; } = [];
    public INetworkConnection? ExpectedConnection { get; private set; }
    public bool RejectTerminalSend { get; init; }

    /// <summary>
    ///     Runs after each packet handed to TrySend is recorded.
    /// </summary>
    public Action<IOutgoingPacket>? OnSent { get; set; }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(long sessionId)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops recording packets of <typeparamref name="T" />, for tests about other packets; TrySend still succeeds.
    /// </summary>
    public StubPacketSendService Ignore<T>()
        where T : IOutgoingPacket
    {
        _ignored.Add(typeof(T));

        return this;
    }

    public bool TrySend(long sessionId, IOutgoingPacket packet)
    {
        if (_ignored.Contains(packet.GetType()))
        {
            Ignored.Add(packet);

            return true;
        }

        SentCount++;
        Sent.Add(packet);
        SentSessionIds.Add(sessionId);
        OnSent?.Invoke(packet);

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
