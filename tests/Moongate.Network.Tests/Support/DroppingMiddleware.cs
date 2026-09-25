using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Network.Tests.Support;

/// <summary>
/// Test middleware that drops every payload by returning <see cref="ReadOnlyMemory{T}.Empty" />,
/// short-circuiting the pipeline.
/// </summary>
public sealed class DroppingMiddleware : INetMiddleware
{
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
    }
}
