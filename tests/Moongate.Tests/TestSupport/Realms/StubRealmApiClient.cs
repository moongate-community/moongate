using System.Net;
using Moongate.Api.Interfaces.Client;
using Moongate.Api.Interfaces.Connections;

namespace Moongate.Tests.TestSupport.Realms;

internal sealed class StubRealmApiClient : IApiClient
{
    private readonly Func<CancellationToken, Task<IApiConnection>> _connect;
    private int _attempts;

    public int Attempts => Volatile.Read(ref _attempts);

    public StubRealmApiClient(Func<CancellationToken, Task<IApiConnection>> connect)
    {
        _connect = connect;
    }

    public Task<IApiConnection> ConnectAsync(IPEndPoint endpoint, string targetHost, string expectedPeerId,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _attempts);
        return _connect(cancellationToken);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
