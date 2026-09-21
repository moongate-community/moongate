using System.Net;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Tests.TestSupport.Connections;

internal sealed class LegacyNetworkConnection : INetworkConnection
{
    public long SessionId => 1;
    public EndPoint? RemoteEndPoint => null;
    public bool IsConnected => false;
    public INetFramer? Framer => null;
    public Task Completion => Task.CompletedTask;

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
