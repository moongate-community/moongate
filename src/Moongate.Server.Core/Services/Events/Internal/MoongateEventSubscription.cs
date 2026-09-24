namespace Moongate.Server.Core.Services.Events.Internal;

internal sealed class MoongateEventSubscription : IDisposable
{
    private readonly MoongateEventRegistration _registration;
    private Action<MoongateEventRegistration>? _unsubscribe;

    public MoongateEventSubscription(Action<MoongateEventRegistration> unsubscribe, MoongateEventRegistration registration)
    {
        _unsubscribe = unsubscribe;
        _registration = registration;
    }

    public void Dispose()
    {
        var unsubscribe = Interlocked.Exchange(ref _unsubscribe, null);
        unsubscribe?.Invoke(_registration);
    }
}
