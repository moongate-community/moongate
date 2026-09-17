using System.Threading.Channels;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class ControlledSendMiddleware : INetMiddleware, IDisposable
{
    private readonly ManualResetEventSlim _release;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> _frames = Channel.CreateUnbounded<byte[]>();

    public Task Entered => _entered.Task;
    public int SendThreadId { get; private set; }
    public bool Fail { get; set; }

    public ControlledSendMiddleware(bool blocked = false)
    {
        _release = new ManualResetEventSlim(!blocked);
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(MoongateTcpClient? client, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(data);
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(MoongateTcpClient? client, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        SendThreadId = System.Environment.CurrentManagedThreadId;
        _entered.TrySetResult();
        // Intentionally block the synchronous prefix to expose accidental sends on the game loop.
        _release.Wait(TimeSpan.FromSeconds(5), CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
        if (Fail) throw new IOException("Controlled send failure.");
        _frames.Writer.TryWrite(data.ToArray());
        return ValueTask.FromResult(data);
    }

    public Task<byte[]> ReadAsync()
    {
        return _frames.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    public void Release()
    {
        _release.Set();
    }

    public void Dispose()
    {
        _release.Set();
        _release.Dispose();
    }
}
