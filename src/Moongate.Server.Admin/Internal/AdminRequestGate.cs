namespace Moongate.Server.Admin.Internal;

internal sealed class AdminRequestGate
{
    private readonly Lock _sync = new();
    private readonly int _capacity;
    private bool _accepting;
    private int _active;
    private TaskCompletionSource _drained = Completed();

    public AdminRequestGate(int capacity) { _capacity = capacity; }
    public void Activate() { lock (_sync) { _accepting = true; } }
    public void StopAccepting() { lock (_sync) { _accepting = false; } }
    public int TryEnter()
    {
        lock (_sync)
        {
            if (!_accepting) { return 14; } // gRPC UNAVAILABLE
            if (_active == _capacity) { return 8; } // gRPC RESOURCE_EXHAUSTED
            if (_active++ == 0) { _drained = new(TaskCreationOptions.RunContinuationsAsynchronously); }
            return 0;
        }
    }
    public void Exit() { lock (_sync) { if (--_active == 0) { _drained.TrySetResult(); } } }
    public Task DrainAsync(CancellationToken token) { lock (_sync) { return _drained.Task.WaitAsync(token); } }
    private static TaskCompletionSource Completed()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
