using Moongate.Network.Client;

namespace Moongate.Network.Interfaces.Middleware;

/// <summary>
/// Transforms raw network bytes for a client connection.
/// </summary>
/// <remarks>
/// Middleware operates on raw bytes and MUST NOT assume any message, packet, or frame
/// semantics. The client's optional framer runs after inbound middleware and before
/// <c>OnDataReceived</c>; protocol parsing remains the consumer's responsibility. Returning
/// <see cref="ReadOnlyMemory{T}.Empty" /> from either method drops the payload and
/// short-circuits the remaining pipeline.
/// <see cref="ProcessAsync" /> is invoked only from a connection's receive loop (serial), and
/// <see cref="ProcessSendAsync" /> only from its send path (serial under the send lock). A stateful
/// send middleware — a protocol that encrypts only part of each packet has nowhere else to do it —
/// therefore consumes its state in exactly the order the bytes reach the socket. The two directions
/// may run concurrently (one each), so an implementation MUST NOT share mutable state between them.
/// Both guarantees are per connection: an instance registered on the server is shared by every
/// connection, so per-connection state must be supplied fresh through <c>ConnectionPipeline</c>.
/// Input memory may be used only until the returned ValueTask completes; do not retain it or
/// return a view over released memory. Event payloads are separate stable copies.
/// Inbound output is bounded by the connection configuration. Raw connections accept at most the
/// receive-buffer size per invocation. Framed connections bound pending data to the maximum frame
/// length plus the receive-buffer size, while applying the frame-length limit to each frame rather
/// than to the combined size of multiple complete frames. Exceeding either budget closes the connection.
/// The send lock is not reentrant. Calling <c>SendAsync</c> on the same client from inside
/// <see cref="ProcessSendAsync" /> deadlocks that connection's send path — silently, and without
/// bound whenever the original caller passed <see cref="CancellationToken.None" />.
/// </remarks>
public interface INetMiddleware
{
    /// <summary>
    /// Transforms an incoming payload before it is dispatched to consumers.
    /// </summary>
    /// <param name="client">Client associated with the payload, if available.</param>
    /// <param name="data">Incoming bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transformed bytes, or <see cref="ReadOnlyMemory{T}.Empty" /> to drop the payload.</returns>
    ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Transforms an outgoing payload before it is written to the socket.
    /// </summary>
    /// <param name="client">Client associated with the payload, if available.</param>
    /// <param name="data">Outgoing bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transformed bytes, or <see cref="ReadOnlyMemory{T}.Empty" /> to drop the payload.</returns>
    ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult(data);
    }
}
