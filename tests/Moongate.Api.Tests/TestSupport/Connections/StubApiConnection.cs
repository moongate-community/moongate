using Moongate.Api.Data.Security;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Api.Tests.TestSupport.Connections;

internal sealed class StubApiConnection : IApiConnection
{
    public long ConnectionId => 1;
    public ApiPeerIdentity Peer { get; }
    public Task Completion => Task.CompletedTask;

    public StubApiConnection(params ushort[] permissions)
    {
        Peer = new("test-peer", permissions);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    public Task<TResponse> RequestAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) where TRequest : IApiRequest<TResponse>
        => throw new NotSupportedException();
}
