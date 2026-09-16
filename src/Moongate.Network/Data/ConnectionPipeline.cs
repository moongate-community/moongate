using Moongate.Network.Interfaces.Codecs;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Network.Data;

/// <summary>
/// Per-connection transport configuration produced by a server factory on each accepted connection.
/// </summary>
public sealed record ConnectionPipeline
{
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
