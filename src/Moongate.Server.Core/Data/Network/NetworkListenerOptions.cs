using System.Net;
using Moongate.Network.Data;

namespace Moongate.Server.Core.Data.Network;

/// <summary>
///     Endpoints and per-connection protocol configuration for a transport service.
/// </summary>
public sealed class NetworkListenerOptions
{
    /// <summary>
    ///     Endpoints to bind. The network service snapshots the list and each endpoint at construction.
    /// </summary>
    public IReadOnlyList<IPEndPoint> Endpoints { get; init; } = Array.Empty<IPEndPoint>();

    /// <summary>
    ///     Creates independent protocol state for every accepted connection; null receives raw bytes.
    /// </summary>
    public Func<ConnectionPipeline>? ConnectionPipelineFactory { get; init; }
}
