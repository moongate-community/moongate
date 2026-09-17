using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Tests.TestSupport.Packets;

internal sealed class FailingCleanupMiddleware : INetMiddleware
{
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(MoongateTcpClient? client, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.Register(() => throw new IOException("Controlled connection cancellation failure."));
        return ValueTask.FromResult(data);
    }
}
