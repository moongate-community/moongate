namespace Moongate.Tests.TestSupport.Events;

/// <summary>
///     Runs an action once when disposed, to remove a test handler.
/// </summary>
internal sealed class EventSubscription : IDisposable
{
    private readonly Action _remove;

    public EventSubscription(Action remove)
    {
        _remove = remove;
    }

    public void Dispose()
    {
        _remove();
    }
}
