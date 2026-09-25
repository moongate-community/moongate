using Moongate.Network.Client;

namespace Moongate.Network.Tests.Support;

/// <summary>
/// Holds the cleanup worker before its first instruction without adding a production test hook.
/// </summary>
public sealed class CleanupExecutionContextGate
{
    private readonly AsyncLocal<MoongateTcpClient?> _context;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public CleanupExecutionContextGate()
    {
        _context = new(OnContextChanged);
    }

    public Task CaptureSend(MoongateTcpClient client, Func<Task> send)
    {
        _context.Value = client;

        try
        {
            return send();
        }
        finally
        {
            // Other sends and the test continuation must not inherit the marker.
            _context.Value = null;
        }
    }

    public void Release()
    {
        _release.TrySetResult();
    }

    private void OnContextChanged(AsyncLocalValueChangedArgs<MoongateTcpClient?> change)
    {
        // The marked sender resumes while still connected. After it fails, RequestClose marks
        // Closing and Task.Run captures this context. Restoring that context on the cleanup worker
        // invokes this callback before CleanupAsync can cancel tokens. The unmarked waiter is free
        // to resume. Context exits (CurrentValue null) never block, so the failed sender can unwind.
        if (change.ThreadContextChanged && change.CurrentValue is { IsConnected: false })
        {
            _entered.TrySetResult();
            _release.Task.GetAwaiter().GetResult();
        }
    }
}
