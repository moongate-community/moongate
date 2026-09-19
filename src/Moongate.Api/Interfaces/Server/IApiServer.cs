using System.Net;
using Moongate.Api.Interfaces.Connections;
namespace Moongate.Api.Interfaces.Server;
/// <summary>Hosts authenticated API connections with bounded request execution.</summary>
public interface IApiServer : IAsyncDisposable
{
    /// <summary>Gets the bound endpoint while accepting connections, otherwise null.</summary>
    IPEndPoint? Endpoint { get; }
    /// <summary>Gets an immutable snapshot of authenticated connections still owned by this host.</summary>
    IReadOnlyList<IApiConnection> Connections { get; }
    /// <summary>Freezes registration and starts a fresh listener generation.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops admission and waits for bounded graceful shutdown. Cancellation only cancels the caller wait.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
