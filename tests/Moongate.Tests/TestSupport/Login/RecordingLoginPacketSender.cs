using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Login;

internal sealed class RecordingLoginPacketSender : ILoginPacketSendService
{
    private readonly List<IOutgoingPacket> _sent = new();

    public IReadOnlyList<IOutgoingPacket> Sent => _sent;
    public bool TerminalResult { get; set; } = true;

    public Task DisconnectAsync(long sessionId)
    {
        return Task.CompletedTask;
    }

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

    public async Task<bool> SendAndDisconnectAsync(
        long sessionId,
        INetworkConnection expectedConnection,
        IOutgoingPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sent.Add(packet);

        if (!TerminalResult)
        {
            return false;
        }

        await expectedConnection.CloseAsync(cancellationToken);

        return true;
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
