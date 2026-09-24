using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Tests.TestSupport.Network;

internal sealed class BlockingFailingCleanupMiddleware : INetMiddleware, IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.Register(
            () =>
            {
                _entered.TrySetResult();

                if (!_release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Cleanup gate was not released.");
                }

                throw new IOException("Controlled gated cleanup failure.");
            }
        );

        return ValueTask.FromResult(data);
    }

    public void Release()
        => _release.Set();

    public void Dispose()
        => _release.Set();
}
