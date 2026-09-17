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

    public Task StartAsync() => _inner.StartAsync();
    public Task StopAsync() => _inner.StopAsync();
    public bool TrySend(long sessionId, IOutgoingPacket packet) => _inner.TrySend(sessionId, packet);
    public Task DisconnectAsync(long sessionId) => _disconnect(sessionId);
}
