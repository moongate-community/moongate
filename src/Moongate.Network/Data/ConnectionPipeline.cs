using Moongate.Network.Client;
using Moongate.Network.Interfaces.Codecs;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Network.Data;

/// <summary>
///     Per-connection transport configuration produced by a server factory on each accepted connection.
/// </summary>
public sealed record ConnectionPipeline
{
    /// <summary>
    ///     Prepares an owned readable/writable stream before callbacks or receive start.
    /// </summary>
    /// <remarks>
    ///     The returned stream must own its input. On failure dispose any wrapper created;
    ///     the transport owns the input stream and socket. Observe the supplied cancellation token.
    /// </remarks>
    public Func<Stream, CancellationToken, ValueTask<Stream>>? PrepareStreamAsync { get; init; }

    /// <summary>
    ///     Installs per-client callbacks after preparation and before receive starts.
    /// </summary>
    public Action<MoongateTcpClient>? ConfigureClient { get; init; }

    public ITransportCodec? Codec { get; init; }

    public IReadOnlyList<INetMiddleware>? Middlewares { get; init; }

    public INetFramer? Framer { get; init; }

    public ConnectionPipeline(
        ITransportCodec? codec = null,
        IReadOnlyList<INetMiddleware>? middlewares = null,
        INetFramer? framer = null
    )
    {
        Codec = codec;
        Middlewares = middlewares;
        Framer = framer;
    }
}
