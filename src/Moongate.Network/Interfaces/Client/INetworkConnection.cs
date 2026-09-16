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
    /// Completes when the connection's receive work has ended.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the connection has closed.</returns>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw bytes to the connected client.
    /// </summary>
    /// <param name="payload">The payload bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the payload has been written.</returns>
    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
