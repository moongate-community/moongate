namespace Moongate.Network.Interfaces.Framing;

/// <summary>
/// Extracts discrete frames from a continuous byte stream, and may transform them in place.
/// </summary>
/// <remarks>
/// A framer is the bridge between the byte-oriented middleware pipeline and a protocol-specific
/// consumer. It is allowed to rewrite the bytes it inspects — a protocol whose packet header is
/// encrypted while its body travels in clear has nowhere else to decrypt, because
/// <c>ITransportCodec</c> runs before any frame boundary is known.
/// That freedom carries one obligation. The same buffer is inspected repeatedly as more bytes
/// arrive across socket reads, so an implementation MUST transform each byte at most once. A framer
/// that decrypts keeps per-frame state — the header is already decoded, the expected frame length is
/// known — and returns <c>false</c> without touching the buffer again until the frame is complete.
/// Re-transforming a header advances a stream cipher's keystream twice and desynchronises the
/// connection.
/// Two transport guarantees make that per-frame state safe to keep. <see cref="TryReadFrame" /> is
/// invoked only from a connection's receive loop (serial), so an implementation never sees
/// concurrent calls for one connection. And offset 0 of the buffer is always the first byte of the
/// frame currently being read: the transport grows its pending buffer by copying the
/// already-transformed bytes across, and consumes a completed frame by moving the remainder down to
/// offset 0. Neither buffer growth nor frame consumption invalidates a framer's cached per-frame
/// state.
/// Throwing from <see cref="TryReadFrame" /> is the supported way to reject a frame — an impossible
/// declared size, for instance, which a decrypting framer can only discover once it has already
/// consumed keystream over the header. The transport logs the exception, raises <c>OnException</c>
/// on the connection and closes it, which is the only correct outcome for a framer whose state can
/// no longer be trusted. Reporting a frame length that is zero, negative, or longer than the pending
/// buffer is rejected the same way, so it is not an alternative signalling channel.
/// A framer holding per-connection state MUST be supplied fresh for each connection through
/// <c>ConnectionPipeline</c>. Stateless framers may be shared.
/// </remarks>
public interface INetFramer
{
    /// <summary>
    /// Tries to read one complete frame from the start of <paramref name="buffer" />, transforming
    /// it in place if the protocol requires it.
    /// </summary>
    /// <param name="buffer">
    /// Accumulated bytes available for inspection. The implementation may write to it, subject to
    /// the once-per-byte rule described on the interface.
    /// </param>
    /// <param name="frameLength">
    /// The number of bytes to consume from the start of the buffer when a complete frame is present.
    /// Undefined when the method returns <c>false</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when <paramref name="buffer" /> starts with a complete frame and
    /// <paramref name="frameLength" /> has been written; <c>false</c> when more bytes are required.
    /// </returns>
    bool TryReadFrame(Span<byte> buffer, out int frameLength);
}
