using Moongate.Api.Data.Security;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Api.Interfaces.Connections;

/// <summary>An authenticated, bidirectional API connection with independent request correlation.</summary>
public interface IApiConnection : IAsyncDisposable
{
    /// <summary>Gets the local connection identifier.</summary>
    long ConnectionId { get; }

    /// <summary>Gets the locally verified remote process identity.</summary>
    ApiPeerIdentity Peer { get; }

    /// <summary>Completes after all owned transport and handler work has terminated.</summary>
    Task Completion { get; }

    /// <summary>Requests closure without awaiting the current callback or handler.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls a registered operation. A timeout after sending does not establish whether execution occurred.</summary>
    Task<TResponse> RequestAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) where TRequest : IApiRequest<TResponse>;
}
