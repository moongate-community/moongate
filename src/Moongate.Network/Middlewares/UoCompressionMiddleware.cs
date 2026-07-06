using System.Buffers;
using Moongate.Network.Compression;
using SquidStd.Network.Client;
using SquidStd.Network.Interfaces.Middleware;

namespace Moongate.Network.Middlewares;

/// <summary>
/// Applies UO transport Huffman compression to outbound payloads once
/// <see cref="Enabled" /> is set. One instance lives per client connection; the login
/// flow flips <see cref="Enabled" /> to <c>true</c> after the game login so the login
/// handshake stays uncompressed. Inbound payloads are never compressed by UO, so
/// <see cref="ProcessAsync" /> is a pass-through.
/// </summary>
public sealed class UoCompressionMiddleware : INetMiddleware
{
    /// <summary>
    /// Whether outbound payloads are compressed. Starts <c>false</c> (login is sent in the
    /// clear) and is set to <c>true</c> by the login flow once the game session begins.
    /// </summary>
    public bool Enabled { get; set; }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        SquidStdTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult(data);
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        SquidStdTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        if (!Enabled || data.IsEmpty)
        {
            return ValueTask.FromResult(data);
        }

        var maxSize = HuffmanEncoder.CalculateMaxCompressedSize(data.Length);

        if (maxSize <= 0)
        {
            return ValueTask.FromResult(data);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(maxSize);

        try
        {
            var compressedLength = HuffmanEncoder.Compress(data.Span, buffer.AsSpan(0, maxSize));

            if (compressedLength <= 0)
            {
                return ValueTask.FromResult(data);
            }

            return ValueTask.FromResult<ReadOnlyMemory<byte>>(buffer.AsMemory(0, compressedLength).ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
