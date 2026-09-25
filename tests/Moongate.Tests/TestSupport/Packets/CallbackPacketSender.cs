using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Packets;

internal sealed class CallbackPacketSender : IPacketSendService
{
    private readonly IPacketSendService _inner;
    private readonly Func<long, Task> _disconnect;

    public CallbackPacketSender(IPacketSendService inner, Func<long, Task> disconnect)
    {
        _inner = inner;
        _disconnect = disconnect;
    }

    public Task DisconnectAsync(long sessionId)
    {
        return _disconnect(sessionId);
    }

    public Task StartAsync()
    {
        return _inner.StartAsync();
    }

    public Task StopAsync()
    {
        return _inner.StopAsync();
    }

    public bool TrySend(long sessionId, IOutgoingPacket packet)
    {
        return _inner.TrySend(sessionId, packet);
    }

    public bool TrySend(long sessionId, INetworkConnection expectedConnection, IOutgoingPacket packet)
    {
        return _inner.TrySend(sessionId, expectedConnection, packet);
    }

    public Task<bool> SendAndDisconnectAsync(
        long sessionId,
        INetworkConnection expectedConnection,
        IOutgoingPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        return _inner.SendAndDisconnectAsync(sessionId, expectedConnection, packet, cancellationToken);
    }
}
