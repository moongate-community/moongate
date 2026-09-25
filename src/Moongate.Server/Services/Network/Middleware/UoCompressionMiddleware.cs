using System.Buffers;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Packets.Compression;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Network.Middleware;

/// <summary>
///     Huffman-compresses what the game server sends on a connection, from the moment its session has
///     <see cref="Moongate.Server.Core.Data.Sessions.NetworkSession.CompressionEnabled" /> set. Until then, and for
///     everything received, bytes pass through unchanged.
/// </summary>
/// <remarks>
///     One instance serves every connection: it keeps no state of its own and asks the session, so the login
///     replies that precede the game login stay uncompressed.
/// </remarks>
public sealed class UoCompressionMiddleware : INetMiddleware
{
    private readonly ISessionService _sessions;

    public UoCompressionMiddleware(ISessionService sessions)
    {
        _sessions = sessions;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult(data);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    ///     The payload cannot be compressed. Sending it as it is would desynchronize the client, so the caller's
    ///     send fails instead.
    /// </exception>
    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        if (data.IsEmpty || client is null || !IsCompressionEnabled(client.SessionId))
        {
            return ValueTask.FromResult(data);
        }

        var maximumSize = HuffmanEncoder.CalculateMaxCompressedSize(data.Length);

        if (maximumSize <= 0)
        {
            throw new InvalidOperationException($"A payload of {data.Length} bytes is too large to compress.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(maximumSize);

        try
        {
            var compressedLength = HuffmanEncoder.Compress(data.Span, buffer.AsSpan(0, maximumSize));

            if (compressedLength <= 0)
            {
                throw new InvalidOperationException($"A payload of {data.Length} bytes could not be compressed.");
            }

            // The pipeline forbids returning memory that is released afterwards, so hand back a copy.
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(buffer.AsMemory(0, compressedLength).ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private bool IsCompressionEnabled(long sessionId)
    {
        return _sessions.TryGet(sessionId, out var session) && session.NetworkSession.CompressionEnabled;
    }
}
