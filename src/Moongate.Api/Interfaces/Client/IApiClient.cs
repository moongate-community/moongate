using System.Net;
using Moongate.Api.Interfaces.Connections;
namespace Moongate.Api.Interfaces.Client;
/// <summary>Owns explicit outgoing authenticated connections. Requests are never replayed automatically.</summary>
public interface IApiClient : IAsyncDisposable
{
    /// <summary>Connects to an endpoint with hostname and locally configured peer identity validation.</summary>
    Task<IApiConnection> ConnectAsync(IPEndPoint endpoint, string targetHost, string expectedPeerId, CancellationToken cancellationToken = default);
}
