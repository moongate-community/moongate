using Moongate.Network.Data.Events;

namespace Moongate.Network.Tests.Support;

internal sealed class BlockingConnectHandler : IDisposable
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ManualResetEventSlim Release { get; } = new();

    public void Handle(object? sender, TcpClientEventArgs args)
    {
        Entered.TrySetResult();

        if (!Release.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The connect handler was not released.");
        }
    }

    public void Dispose()
    {
        Release.Set();
        Release.Dispose();
    }
}
