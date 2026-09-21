namespace Moongate.Server.Core.Services.Events.Internal;

internal sealed class MoongateEventSubscription : IDisposable
{
    private readonly Type _eventType;
    private readonly MoongateEventRegistration _registration;
    private MoongateEventBus? _eventBus;

    public MoongateEventSubscription(MoongateEventBus eventBus, Type eventType, MoongateEventRegistration registration)
    {
        _eventBus = eventBus;
        _eventType = eventType;
        _registration = registration;
    }

    public void Dispose()
    {
        var eventBus = Interlocked.Exchange(ref _eventBus, null);
        eventBus?.Unsubscribe(_eventType, _registration);
    }
}
