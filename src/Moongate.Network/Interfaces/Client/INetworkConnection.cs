using System.Net;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Interfaces.Client;

/// <summary>
/// Represents a connected network client independently from the underlying transport.
/// </summary>
public interface INetworkConnection
{
    /// <summary>
    /// Unique connection identifier assigned by the transport.
    /// </summary>
    long SessionId { get; }

    /// <summary>
    /// Remote endpoint when the transport exposes one.
    /// </summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>Gets the local endpoint when exposed by the transport.</summary>
    /// <remarks>Existing implementations without local endpoint metadata return null.</remarks>
    EndPoint? LocalEndPoint => null;

    /// <summary>
    /// Indicates whether the connection is still open.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// The framer delimiting this connection's inbound bytes, when one was supplied.
    /// </summary>
    /// <remarks>
    /// Exposed because a framer that transforms bytes may hold per-connection state that the
    /// application must configure at a protocol transition. Sharing the same instance keeps the
    /// inbound framing state aligned with its matching outbound transform.
    /// </remarks>
    INetFramer? Framer { get; }

    /// <summary>
    /// Completes after receive work, all admitted sends and resource cleanup have ended.
    /// Available before Start; faults if cleanup fails.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Requests idempotent connection closure without waiting for callbacks or cleanup.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// Safe from synchronous callbacks, as is synchronous Dispose. Do not block a callback waiting
    /// for Completion or DisposeAsync of its client or server, and do not use async-void handlers.
    /// </remarks>
    /// <returns>A task that completes when closure has been requested.</returns>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw bytes to the connected client.
    /// </summary>
    /// <param name="payload">Payload bytes, which the caller must keep immutable until completion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// Closed connections reject sends, including empty payloads. Empty payloads on open connections
    /// are a no-op. Middleware, codec and writes are serialized together, without a FIFO guarantee
    /// between simultaneous callers. Cancellation before entering the send gate leaves the connection
    /// usable; failure after transformation or writing begins closes it. Send failures reach the caller.
    /// There is no application queue or admission limit; producers must await their sends.
    /// </remarks>
    /// <returns>A task that completes when the payload has been written.</returns>
    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
