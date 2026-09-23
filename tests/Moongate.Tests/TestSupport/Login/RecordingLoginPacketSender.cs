using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Login;

internal sealed class RecordingLoginPacketSender : ILoginPacketSendService
{
    private readonly List<IOutgoingPacket> _sent = new();

    public IReadOnlyList<IOutgoingPacket> Sent => _sent;

    public Task DisconnectAsync(long sessionId)
        => Task.CompletedTask;

    public bool TrySend(long sessionId, IOutgoingPacket packet)
    {
        _sent.Add(packet);
        return true;
    }

    public bool TrySend(long sessionId, INetworkConnection expectedConnection, IOutgoingPacket packet)
    {
        _sent.Add(packet);
        return true;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
